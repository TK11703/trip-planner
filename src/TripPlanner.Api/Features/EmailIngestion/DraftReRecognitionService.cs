using Microsoft.Extensions.Logging;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Brings a draft that predates transport recognition up to date, the first time the traveler
/// opens it (FR-045).
///
/// ⚠ This deliberately does not reuse <see cref="RelayMessageProcessor.ReprocessAsync"/>. That
/// path ends in <c>InsertAsync</c>, so pointing it at an existing draft would add a second one to
/// the queue every time a legacy draft was opened — a duplicate the traveler sees, not a test
/// failure. Everything here is an <c>UPDATE</c>.
///
/// A draft is re-examined at most once whatever the outcome: recognition answering sets
/// <c>current</c>, recognition being unavailable sets <c>unavailable</c> (FR-047). Either way the
/// draft stays reviewable and confirmable on the item path, because a booking the recognizer
/// cannot read is still a booking the traveler can file by hand.
/// </summary>
public sealed partial class DraftReRecognitionService
{
    private readonly IParsedItemDraftRepository _drafts;
    private readonly IInboxEmailRepository _emails;
    private readonly IEmailAttachmentRepository _attachments;
    private readonly Func<IItemRecognizer> _recognizerFactory;
    private readonly ILogger<DraftReRecognitionService> _logger;

    public DraftReRecognitionService(
        IParsedItemDraftRepository drafts,
        IInboxEmailRepository emails,
        IEmailAttachmentRepository attachments,
        Func<IItemRecognizer> recognizerFactory,
        ILogger<DraftReRecognitionService> logger)
    {
        _drafts = drafts;
        _emails = emails;
        _attachments = attachments;
        _recognizerFactory = recognizerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Returns the draft as the traveler should now see it, or null when there is no such pending
    /// draft of theirs. A draft that has already been through this returns unchanged without
    /// calling the provider, so reopening the review screen costs nothing.
    /// </summary>
    public async Task<ParsedItemDraftRecord?> ReRecognizeAsync(Guid draftId, string userId, CancellationToken ct = default)
    {
        var draft = await _drafts.GetByIdAsync(draftId, userId, ct);
        if (draft is null || draft.ReviewStatus != "pending_review")
        {
            return null;
        }

        if (draft.TransportRecognitionState != DraftRecognitionStates.Pending)
        {
            return draft;
        }

        var email = await _emails.GetByIdAsync(draft.InboxEmailId, userId, ct);
        if (email is null)
        {
            // The message is gone but the draft is not. Settle the state so the traveler is not
            // asked to wait for this again, and leave the draft exactly as it was.
            LogSourceMissing(draftId);
            return await MarkUnavailableAsync(draftId, userId, ct) ?? draft;
        }

        RecognitionResult recognition;
        try
        {
            // Resolved here rather than injected, so that a recognizer which cannot even be
            // constructed — an unconfigured endpoint, a credential that will not load — is
            // treated as the outage it is. Injecting it would make that failure happen during
            // dependency resolution, before this method runs, and the draft would be left
            // `pending` forever: every open would retry, and every retry would fail the same way
            // (FR-047).
            var recognizer = _recognizerFactory();

            var attachments = await _attachments.GetForEmailAsync(draft.InboxEmailId, ct);
            var attachmentText = attachments
                .Select(a => a.ExtractedText)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();

            var assembled = EmailTextAssembler.Assemble(email.Subject, email.BodyText, attachmentText!);
            recognition = await recognizer.RecognizeAsync(draft.InboxEmailId, userId, assembled, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogReRecognitionFailed(ex, draftId);
            return await MarkUnavailableAsync(draftId, userId, ct) ?? draft;
        }

        if (recognition.ParseStatus != InboxEmailParseStatus.Parsed || recognition.Drafts.Count == 0)
        {
            LogNothingRecognized(draftId);
            return await MarkUnavailableAsync(draftId, userId, ct) ?? draft;
        }

        var match = SelectMatch(draft, recognition.Drafts);
        var merge = new DraftRecognitionMerge(
            ProposedOutcome: match.ProposedOutcome,
            Origin: match.Origin,
            Destination: match.Destination,
            TransportationMode: TransportationModes.Normalize(match.TransportationMode),
            TravelCost: match.TravelCost,
            TravelCostCurrency: match.TravelCostCurrency,
            Title: match.Title,
            Location: match.Location,
            ConfirmationCode: match.ConfirmationCode,
            StartLocal: match.StartLocal,
            StartTimeZoneId: match.StartTimeZoneId,
            TransportRecognitionState: DraftRecognitionStates.Current);

        return await _drafts.MergeRecognitionAsync(draftId, userId, merge, ct) ?? draft;
    }

    private Task<ParsedItemDraftRecord?> MarkUnavailableAsync(Guid draftId, string userId, CancellationToken ct)
        => _drafts.MergeRecognitionAsync(draftId, userId, new DraftRecognitionMerge(
            null, null, null, null, null, null, null, null, null, null, null,
            DraftRecognitionStates.Unavailable), ct);

    /// <summary>
    /// One message can describe several bookings, so the re-read has to be attributed back to the
    /// draft it belongs to. A shared confirmation code is the strongest signal; failing that the
    /// nearest start; failing that, a single recognized booking can only be this one.
    /// </summary>
    private static NewParsedItemDraft SelectMatch(ParsedItemDraftRecord draft, IReadOnlyList<NewParsedItemDraft> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        if (!string.IsNullOrWhiteSpace(draft.ConfirmationCode))
        {
            var byCode = candidates.FirstOrDefault(c =>
                string.Equals(c.ConfirmationCode, draft.ConfirmationCode, StringComparison.OrdinalIgnoreCase));
            if (byCode is not null) return byCode;
        }

        if (draft.StartLocal is { } start)
        {
            var nearest = candidates
                .Where(c => c.StartLocal is not null)
                .OrderBy(c => Math.Abs((c.StartLocal!.Value - start).Ticks))
                .FirstOrDefault();
            if (nearest is not null) return nearest;
        }

        return candidates[0];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Re-recognition found no usable booking for draft {DraftId}.")]
    private partial void LogNothingRecognized(Guid draftId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Re-recognition failed for draft {DraftId}; leaving it on the item path.")]
    private partial void LogReRecognitionFailed(Exception exception, Guid draftId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Draft {DraftId} has no source message to re-read.")]
    private partial void LogSourceMissing(Guid draftId);
}
