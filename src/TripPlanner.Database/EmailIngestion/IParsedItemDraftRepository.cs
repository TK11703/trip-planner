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
    Guid? TrackedItemId = null);

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
    double Confidence);

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
    string? Notes);

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
    Task<bool> SetReviewStatusAsync(Guid parsedItemDraftId, string userId, string reviewStatus, Guid? trackedItemId = null, CancellationToken ct = default);

    /// <summary>Every leg on every trip the caller can edit, ordered by leg start.</summary>
    Task<IReadOnlyList<PlacementCandidateLeg>> GetPlacementCandidateLegsAsync(string userId, string? callerEmail, CancellationToken ct = default);
}
