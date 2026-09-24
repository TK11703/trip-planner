using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>An HTTP status code paired with the conclusive body the relay branches on.</summary>
public sealed record RelayIngestionResult(int StatusCode, IngestRelayMessageResponse Body);

/// <summary>
/// Runs the whole ingestion pipeline for one relayed message, synchronously, inside the caller's
/// request. Nothing is deferred to a later pass, so every call ends in a conclusive outcome.
///
/// The order below is normative (see contracts/relay-ingestion-endpoint.md):
/// validate → resolve traveler from sender → compute dedupe hash → decode attachments →
/// insert message → insert attachments → recognize → persist drafts and notify → return.
/// </summary>
public sealed partial class RelayMessageProcessor
{
    private readonly IInboxEmailRepository _emails;
    private readonly IEmailAttachmentRepository _attachments;
    private readonly IParsedItemDraftRepository _drafts;
    private readonly EmailSenderResolver _senderResolver;
    private readonly EmailAttachmentTextExtractor _extractor;
    private readonly EmailDeduplicationService _dedupe;
    private readonly IItemRecognizer _parser;
    private readonly INotificationService _notifications;
    private readonly ILogger<RelayMessageProcessor> _logger;

    public RelayMessageProcessor(
        IInboxEmailRepository emails,
        IEmailAttachmentRepository attachments,
        IParsedItemDraftRepository drafts,
        EmailSenderResolver senderResolver,
        EmailAttachmentTextExtractor extractor,
        EmailDeduplicationService dedupe,
        IItemRecognizer parser,
        INotificationService notifications,
        ILogger<RelayMessageProcessor> logger)
    {
        _emails = emails;
        _attachments = attachments;
        _drafts = drafts;
        _senderResolver = senderResolver;
        _extractor = extractor;
        _dedupe = dedupe;
        _parser = parser;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<RelayIngestionResult> IngestAsync(IngestRelayMessageRequest? request, CancellationToken ct = default)
    {
        // 1. Validate the payload.
        if (request is null)
        {
            return Error(StatusCodes.Status400BadRequest, EmailIngestionOutcome.InvalidRequest, "A message payload is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Sender))
        {
            return Error(StatusCodes.Status400BadRequest, EmailIngestionOutcome.InvalidRequest, "'sender' is required.");
        }

        if (request.Subject is null)
        {
            return Error(StatusCodes.Status400BadRequest, EmailIngestionOutcome.InvalidRequest, "'subject' is required; use an empty string when the message has none.");
        }

        if (request.ReceivedAt == default)
        {
            return Error(StatusCodes.Status400BadRequest, EmailIngestionOutcome.InvalidRequest, "'receivedAt' is required.");
        }

        var attachments = request.Attachments ?? [];
        foreach (var attachment in attachments)
        {
            if (attachment is null || string.IsNullOrWhiteSpace(attachment.FileName) || string.IsNullOrWhiteSpace(attachment.ContentType) || attachment.ContentBase64 is null)
            {
                return Error(StatusCodes.Status400BadRequest, EmailIngestionOutcome.InvalidRequest, "Each attachment requires 'fileName', 'contentType', and 'contentBase64'.");
            }
        }

        // 2. Decode attachments and enforce the size caps.
        var decoded = new List<DecodedAttachment>(attachments.Count);
        long totalBytes = 0;
        foreach (var attachment in attachments)
        {
            var result = _extractor.Decode(attachment);
            switch (result.Outcome)
            {
                case AttachmentDecodeOutcome.InvalidBase64:
                    return Error(StatusCodes.Status400BadRequest, EmailIngestionOutcome.InvalidRequest, $"Attachment '{result.FileName}' is not valid base64.");
                case AttachmentDecodeOutcome.TooLarge:
                    return Error(StatusCodes.Status413PayloadTooLarge, EmailIngestionOutcome.TooLarge, $"Attachment '{result.FileName}' exceeds the {EmailAttachmentTextExtractor.MaxAttachmentBytes / (1024 * 1024)} MB limit.");
            }

            totalBytes += result.Content.Length;
            if (totalBytes > EmailAttachmentTextExtractor.MaxRequestBytes)
            {
                return Error(StatusCodes.Status413PayloadTooLarge, EmailIngestionOutcome.TooLarge, "The message exceeds the request size limit.");
            }

            decoded.Add(result);
        }

        // 3. Attribute the message to exactly one traveler. The caller's identity is never used.
        var resolution = await _senderResolver.ResolveAsync(request.Sender, ct);
        if (resolution.Outcome != SenderResolutionOutcome.Resolved)
        {
            LogSenderNotAttributed(resolution.Outcome);
            var detail = resolution.Outcome == SenderResolutionOutcome.Ambiguous
                ? "The sender address matches more than one traveler."
                : "The sender address does not match any traveler.";
            return Error(StatusCodes.Status422UnprocessableEntity, EmailIngestionOutcome.UnknownSender, detail);
        }

        var userId = resolution.UserId!;

        // 4. Establish the body text and confirm there is something to work with.
        var bodyText = !string.IsNullOrWhiteSpace(request.BodyText)
            ? request.BodyText
            : EmailAttachmentTextExtractor.StripMarkup(request.BodyHtml);
        var attachmentText = decoded.Select(a => a.ExtractedText).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

        if (string.IsNullOrWhiteSpace(bodyText) && attachmentText.Length == 0 && string.IsNullOrWhiteSpace(request.Subject))
        {
            return Error(StatusCodes.Status400BadRequest, EmailIngestionOutcome.InvalidRequest, "The message carries no usable text.");
        }

        // 5. Compute the dedupe hash and store the message. A collision means the relay has already
        //    delivered this message; that is a success, not a failure.
        var dedupeHash = _dedupe.ComputeHash(request.MessageId, request.Sender, request.Subject, request.ReceivedAt, bodyText);

        // The message is stored before recognition runs so a redelivery is caught immediately.
        // 'failed' is the conservative terminal status it carries until recognition concludes —
        // no row is ever persisted in a deferred state.
        var stored = await _emails.InsertAsync(new NewInboxEmail(
            UserId: userId,
            MessageId: string.IsNullOrWhiteSpace(request.MessageId) ? null : request.MessageId.Trim(),
            Sender: resolution.NormalizedSender,
            Recipient: string.IsNullOrWhiteSpace(request.Recipient) ? null : request.Recipient.Trim(),
            Subject: request.Subject,
            BodyText: bodyText ?? string.Empty,
            BodyHtml: request.BodyHtml,
            ReceivedAt: request.ReceivedAt,
            DedupeHash: dedupeHash,
            ParseStatus: InboxEmailParseStatus.Failed), ct);

        if (stored is null)
        {
            LogDuplicateMessage(userId);
            return new RelayIngestionResult(StatusCodes.Status200OK,
                new IngestRelayMessageResponse(EmailIngestionOutcome.Duplicate, null, [], "This message was already ingested."));
        }

        // 6. Retain the attachments against the stored message.
        foreach (var attachment in decoded)
        {
            await _attachments.InsertAsync(new NewEmailAttachment(
                stored.InboxEmailId, attachment.FileName, attachment.ContentType, attachment.Content, attachment.ExtractedText), ct);
        }

        // 7-9. Recognize, persist drafts, notify, and report the outcome.
        var assembled = AssembleText(request.Subject, bodyText, attachmentText!);
        return await RecognizeAndPersistAsync(stored.InboxEmailId, userId, assembled, ct);
    }

    /// <summary>
    /// Re-runs recognition against an already-stored message and its attachments, producing the
    /// same conclusive outcome shape as ingestion.
    /// </summary>
    public async Task<RelayIngestionResult> ReprocessAsync(InboxEmailRecord email, CancellationToken ct = default)
    {
        var attachments = await _attachments.GetForEmailAsync(email.InboxEmailId, ct);
        var attachmentText = attachments.Select(a => a.ExtractedText).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
        var assembled = AssembleText(email.Subject, email.BodyText, attachmentText!);
        return await RecognizeAndPersistAsync(email.InboxEmailId, email.UserId, assembled, ct);
    }

    private async Task<RelayIngestionResult> RecognizeAndPersistAsync(Guid inboxEmailId, string userId, string assembledText, CancellationToken ct)
    {
        var recognition = await _parser.RecognizeAsync(inboxEmailId, userId, assembledText, ct);

        if (recognition.ParseStatus == InboxEmailParseStatus.Failed)
        {
            await _emails.UpdateParseStatusAsync(inboxEmailId, userId, InboxEmailParseStatus.Failed, ct);
            LogRecognitionFailed(inboxEmailId, userId);
            return new RelayIngestionResult(StatusCodes.Status502BadGateway,
                new IngestRelayMessageResponse(EmailIngestionOutcome.ProcessingFailed, inboxEmailId, [], "Recognition is unavailable; retry later."));
        }

        var draftIds = new List<Guid>();
        foreach (var draft in recognition.Drafts)
        {
            var record = await _drafts.InsertAsync(draft, ct);
            if (record is not null)
            {
                draftIds.Add(record.ParsedItemDraftId);
            }
        }

        var parseStatus = draftIds.Count > 0 ? InboxEmailParseStatus.Parsed : InboxEmailParseStatus.Unsupported;
        await _emails.UpdateParseStatusAsync(inboxEmailId, userId, parseStatus, ct);

        if (draftIds.Count == 0)
        {
            LogNothingRecognized(inboxEmailId, userId);
            return new RelayIngestionResult(StatusCodes.Status200OK,
                new IngestRelayMessageResponse(EmailIngestionOutcome.NoContent, inboxEmailId, [], "No trip item could be recognized."));
        }

        await _notifications.CreateAsync(new NewNotification(
            RecipientUserId: userId,
            Category: EmailIngestionNotificationKeys.ParsedEmailCategory,
            Kind: NotificationKind.Actionable,
            TargetType: NotificationTargetType.Person,
            RelatedTripId: null,
            Title: draftIds.Count == 1 ? "New trip item ready to review" : $"{draftIds.Count} trip items ready to review",
            Message: "A relayed email was processed. Review and confirm the extracted items.",
            SourceEventKey: $"email-parsed:{inboxEmailId}"), ct);

        LogDraftsCreated(inboxEmailId, userId, draftIds.Count);
        return new RelayIngestionResult(StatusCodes.Status200OK,
            new IngestRelayMessageResponse(EmailIngestionOutcome.Parsed, inboxEmailId, draftIds, null));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected relayed message: sender could not be attributed ({Outcome}).")]
    private partial void LogSenderNotAttributed(SenderResolutionOutcome outcome);

    [LoggerMessage(Level = LogLevel.Information, Message = "Relayed message for traveler {UserId} was already ingested; no new drafts created.")]
    private partial void LogDuplicateMessage(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Recognition failed for inbox email {InboxEmailId} (traveler {UserId}).")]
    private partial void LogRecognitionFailed(Guid inboxEmailId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inbox email {InboxEmailId} (traveler {UserId}) contained nothing recognizable.")]
    private partial void LogNothingRecognized(Guid inboxEmailId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inbox email {InboxEmailId} (traveler {UserId}) produced {DraftCount} draft(s).")]
    private partial void LogDraftsCreated(Guid inboxEmailId, string userId, int draftCount);

    private static string AssembleText(string subject, string? bodyText, string[] attachmentText)
        => EmailTextAssembler.Assemble(subject, bodyText, attachmentText);

    private static RelayIngestionResult Error(int statusCode, string status, string detail)
        => new(statusCode, new IngestRelayMessageResponse(status, null, [], detail));
}
