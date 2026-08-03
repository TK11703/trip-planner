namespace TripPlanner.Contracts.EmailIngestion;

/// <summary>A raw email received by the trip inbox, as returned to the API consumer.</summary>
public sealed record InboxEmailDto(
    Guid InboxEmailId,
    string Sender,
    string Subject,
    DateTimeOffset ReceivedAt,
    ParseStatus ParseStatus);

/// <summary>A structured event extracted from an inbox email, awaiting user review.</summary>
public sealed record ParsedEventDraftDto(
    Guid ParsedEventDraftId,
    Guid InboxEmailId,
    Guid? TripId,
    Guid? TripLegId,
    string? EventType,
    string? Title,
    string? Location,
    DateTime? StartLocal,
    string? StartTimeZoneId,
    DateTime? EndLocal,
    string? EndTimeZoneId,
    string? ConfirmationCode,
    string? Notes,
    double Confidence,
    ReviewStatus ReviewStatus,
    DateTimeOffset CreatedAt);

/// <summary>Request to update editable fields of a parsed event draft.</summary>
public sealed record UpdateParsedEventDraftRequest(
    Guid? TripId,
    Guid? TripLegId,
    string? EventType,
    string? Title,
    string? Location,
    DateTime? StartLocal,
    string? StartTimeZoneId,
    DateTime? EndLocal,
    string? EndTimeZoneId,
    string? ConfirmationCode,
    string? Notes);

/// <summary>Response returned after confirming a draft (the promoted event id).</summary>
public sealed record ConfirmParsedEventDraftResponse(Guid TrackedItemId, Guid TripId, Guid TripLegId);

/// <summary>A page of inbox emails.</summary>
public sealed record InboxEmailListResponse(IReadOnlyList<InboxEmailDto> Items);

/// <summary>A page of pending drafts.</summary>
public sealed record ParsedEventDraftListResponse(IReadOnlyList<ParsedEventDraftDto> Items);

/// <summary>A file delivered alongside a relayed email.</summary>
public sealed record RelayEmailAttachment(string FileName, string ContentType, string ContentBase64);

/// <summary>
/// A single email message handed to the API by the external automation relay (Logic App).
/// The relay authenticates as an application; the owning traveler is resolved from
/// <paramref name="Sender"/>, never from the caller's identity.
/// </summary>
public sealed record IngestRelayMessageRequest(
    string? MessageId,
    string Sender,
    string? Recipient,
    string Subject,
    DateTimeOffset ReceivedAt,
    string? BodyText,
    string? BodyHtml,
    IReadOnlyList<RelayEmailAttachment>? Attachments);

/// <summary>The conclusive outcome of an ingestion attempt. Never reports deferred work.</summary>
public sealed record IngestRelayMessageResponse(
    string Status,
    Guid? InboxEmailId,
    IReadOnlyList<Guid> DraftIds,
    string? Detail);

/// <summary>Outcome values returned by the relay ingestion endpoint.</summary>
public static class EmailIngestionOutcome
{
    public const string Parsed = "parsed";
    public const string NoContent = "no_content";
    public const string Duplicate = "duplicate";
    public const string InvalidRequest = "invalid_request";
    public const string TooLarge = "too_large";
    public const string UnknownSender = "unknown_sender";
    public const string ProcessingFailed = "processing_failed";
}
