namespace TripPlanner.Database.EmailIngestion;

/// <summary>Persisted representation of a parsed item draft.</summary>
public sealed record ParsedItemDraftRecord(
    Guid ParsedItemDraftId,
    Guid InboxEmailId,
    string UserId,
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
    string ReviewStatus,
    DateTimeOffset CreatedAtUtc,
    // The timeline item this draft became once it was confirmed, so the path from a forwarded
    // email to an itinerary entry stays traceable (FR-025). Null until then.
    Guid? TrackedItemId = null,
    // What this draft proposes to become. 'item' keeps the behaviour every existing row had.
    string ProposedOutcome = DraftOutcomes.Item,
    string? Origin = null,
    string? Destination = null,
    string? TransportationMode = null,
    decimal? TravelCost = null,
    string? TravelCostCurrency = null,
    // The leg this draft became, the leg-outcome counterpart of TrackedItemId (FR-038).
    Guid? CreatedTripLegId = null,
    string TransportRecognitionState = DraftRecognitionStates.Current,
    // Fields the traveler has supplied by hand, which re-recognition must not overwrite. A field
    // named here is left alone even when null, so a deliberately cleared value stays cleared
    // (FR-046).
    IReadOnlyList<string>? TravelerEditedFields = null);

/// <summary>The values <c>proposed_outcome</c> may take, matching the database check.</summary>
public static class DraftOutcomes
{
    public const string Item = "item";
    public const string Leg = "leg";
}

/// <summary>The values <c>transport_recognition_state</c> may take, matching the database check.</summary>
public static class DraftRecognitionStates
{
    public const string Current = "current";
    public const string Pending = "pending";
    public const string Unavailable = "unavailable";
}

/// <summary>A new parsed item draft to insert.</summary>
public sealed record NewParsedItemDraft(
    Guid InboxEmailId,
    string UserId,
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
    string ProposedOutcome = DraftOutcomes.Item,
    string? Origin = null,
    string? Destination = null,
    string? TransportationMode = null,
    decimal? TravelCost = null,
    string? TravelCostCurrency = null);

/// <summary>
/// What re-recognition proposes for a draft that predates transport recognition. Applied under
/// the FR-046 rule: a field lands only where the draft has none and the traveler has not touched
/// it. The SQL decides what survives, not the caller.
/// </summary>
public sealed record DraftRecognitionMerge(
    string? ProposedOutcome,
    string? Origin,
    string? Destination,
    string? TransportationMode,
    decimal? TravelCost,
    string? TravelCostCurrency,
    string? Title,
    string? Location,
    string? ConfirmationCode,
    DateTime? StartLocal,
    string? StartTimeZoneId,
    // 'current' once recognition has answered, 'unavailable' when it could not (FR-047). Always
    // written, so a draft is re-examined at most once either way.
    string TransportRecognitionState);

/// <summary>Fields the user can edit on a pending draft.</summary>
public sealed record DraftUpdate(
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
    string ProposedOutcome = DraftOutcomes.Item,
    string? Origin = null,
    string? Destination = null,
    string? TransportationMode = null,
    decimal? TravelCost = null);

/// <summary>
/// A trip the caller can add items to, paired with one of its legs. The leg fields are null when
/// the trip has no legs at all, which lets a draft that lands inside the trip's dates be told
/// apart from one that lands outside every trip. <paramref name="LegEnd"/> is null for an
/// open-ended leg.
/// </summary>
public sealed record PlacementCandidateLeg(
    Guid TripId,
    string TripName,
    DateOnly TripStart,
    DateOnly TripEnd,
    Guid? TripLegId,
    string? LegTitle,
    DateTimeOffset? LegStart,
    DateTimeOffset? LegEnd);

public interface IParsedItemDraftRepository
{
    Task<ParsedItemDraftRecord?> InsertAsync(NewParsedItemDraft draft, CancellationToken ct = default);
    Task<IReadOnlyList<ParsedItemDraftRecord>> GetPendingAsync(string userId, CancellationToken ct = default);
    Task<ParsedItemDraftRecord?> GetByIdAsync(Guid parsedItemDraftId, string userId, CancellationToken ct = default);
    Task<ParsedItemDraftRecord?> UpdateAsync(Guid parsedItemDraftId, string userId, DraftUpdate update, CancellationToken ct = default);

    /// <summary>
    /// Marks a draft resolved and records what it became. At most one of
    /// <paramref name="trackedItemId"/> and <paramref name="createdTripLegId"/> is supplied — a
    /// draft becomes an item or a leg, never both (FR-014, FR-038, FR-041).
    /// </summary>
    Task<bool> SetReviewStatusAsync(
        Guid parsedItemDraftId,
        string userId,
        string reviewStatus,
        Guid? trackedItemId = null,
        string? proposedOutcome = null,
        Guid? createdTripLegId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Applies re-recognition to a pending draft under the FR-046 rule, returning the updated
    /// record. An <c>UPDATE</c> only — this never inserts, so re-examining a draft cannot add a
    /// second one to the queue.
    /// </summary>
    Task<ParsedItemDraftRecord?> MergeRecognitionAsync(
        Guid parsedItemDraftId,
        string userId,
        DraftRecognitionMerge merge,
        CancellationToken ct = default);

    /// <summary>Every leg on every trip the caller can edit, ordered by leg start.</summary>
    Task<IReadOnlyList<PlacementCandidateLeg>> GetPlacementCandidateLegsAsync(string userId, string? callerEmail, CancellationToken ct = default);
}
