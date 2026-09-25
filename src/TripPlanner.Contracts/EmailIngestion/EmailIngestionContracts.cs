namespace TripPlanner.Contracts.EmailIngestion;

/// <summary>A raw email received by the trip inbox, as returned to the API consumer.</summary>
public sealed record InboxEmailDto(
    Guid InboxEmailId,
    string Sender,
    string Subject,
    DateTimeOffset ReceivedAt,
    ParseStatus ParseStatus);

/// <summary>
/// Why a draft can or cannot be placed on a trip leg. Computed per request from the legs that
/// exist at the moment of the call; never persisted.
/// </summary>
public enum DraftPlacementStatus
{
    /// <summary>Exactly one editable leg covers the draft's timeframe.</summary>
    Matched = 0,

    /// <summary>Two or more editable legs cover the draft's timeframe; the traveler must choose.</summary>
    Ambiguous = 1,

    /// <summary>The draft falls inside an accessible trip's dates but no leg covers it.</summary>
    NoLegCovers = 2,

    /// <summary>The draft falls outside every accessible trip's dates.</summary>
    OutsideTripDates = 3,

    /// <summary>The draft has no usable start, so containment cannot be judged.</summary>
    InsufficientData = 4
}

/// <summary>A trip leg the draft could be placed on, with enough context to name it in the UI.</summary>
public sealed record PlacementCandidate(
    Guid TripId,
    string TripName,
    Guid TripLegId,
    string LegTitle,
    DateTimeOffset LegStart,
    DateTimeOffset? LegEnd);

/// <summary>The computed placement suggestion for a pending draft.</summary>
public sealed record DraftPlacement(
    DraftPlacementStatus Status,
    Guid? SuggestedTripId,
    Guid? SuggestedTripLegId,
    IReadOnlyList<PlacementCandidate> Candidates);

/// <summary>
/// What a draft proposes to become when it is confirmed. Recognition proposes it, the traveler
/// may change it, and it is binding at confirmation (FR-008, FR-024).
/// </summary>
public enum DraftOutcome
{
    /// <summary>A tracked item on the timeline. Zero so an unset value keeps today's behaviour.</summary>
    Item = 0,

    /// <summary>A trip leg — how the traveler gets from one place to the next.</summary>
    Leg = 1
}

/// <summary>
/// Whether a draft has been through transport recognition. Drafts that predate transport
/// recognition are <see cref="Pending"/> until the review screen asks for them to be
/// re-examined (FR-045).
/// </summary>
public enum DraftRecognitionState
{
    /// <summary>Recognized with the current rules; nothing further to do.</summary>
    Current = 0,

    /// <summary>Predates transport recognition and has not been re-examined yet.</summary>
    Pending = 1,

    /// <summary>Re-examination was attempted and the recognizer was unavailable (FR-047).</summary>
    Unavailable = 2
}

/// <summary>A structured item extracted from an inbox email, awaiting user review.</summary>
public sealed record ParsedItemDraftDto(
    Guid ParsedItemDraftId,
    Guid InboxEmailId,
    Guid? TripId,
    Guid? TripLegId,
    string? ItemType,
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
    DateTimeOffset CreatedAt,
    DraftPlacement? Placement = null,
    DraftOutcome ProposedOutcome = DraftOutcome.Item,
    string? Origin = null,
    string? Destination = null,
    string? TransportationMode = null,
    decimal? TravelCost = null,
    // Recognized only so the review screen can label the amount. A leg has no currency of its
    // own, so this is never written to one (FR-010).
    string? TravelCostCurrency = null,
    // The leg this draft became, once confirmed as a leg (FR-038). Null until then, and null
    // again if that leg is later deleted.
    Guid? CreatedTripLegId = null,
    DraftRecognitionState TransportRecognitionState = DraftRecognitionState.Current);

/// <summary>Request to update editable fields of a parsed item draft.</summary>
public sealed record UpdateParsedItemDraftRequest(
    Guid? TripId,
    Guid? TripLegId,
    string? ItemType,
    string? Title,
    string? Location,
    DateTime? StartLocal,
    string? StartTimeZoneId,
    DateTime? EndLocal,
    string? EndTimeZoneId,
    string? ConfirmationCode,
    string? Notes,
    DraftOutcome ProposedOutcome = DraftOutcome.Item,
    string? Origin = null,
    string? Destination = null,
    string? TransportationMode = null,
    decimal? TravelCost = null);

/// <summary>
/// What a confirmed draft became. Exactly one of <paramref name="TrackedItemId"/> and
/// <paramref name="CreatedTripLegId"/> is set, matching <paramref name="Outcome"/> (FR-014).
/// <paramref name="TripLegId"/> is where an item was placed, which is a different question from
/// what was created, so the two are kept apart.
/// </summary>
public sealed record ConfirmParsedItemDraftResponse(
    DraftOutcome Outcome,
    Guid TripId,
    Guid? TrackedItemId,
    Guid? TripLegId,
    Guid? CreatedTripLegId);

/// <summary>A page of inbox emails.</summary>
public sealed record InboxEmailListResponse(IReadOnlyList<InboxEmailDto> Items);

/// <summary>A page of pending drafts.</summary>
public sealed record ParsedItemDraftListResponse(IReadOnlyList<ParsedItemDraftDto> Items);

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
