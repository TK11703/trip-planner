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
    DateTimeOffset CreatedAtUtc);

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

public interface IParsedItemDraftRepository
{
    Task<ParsedItemDraftRecord?> InsertAsync(NewParsedItemDraft draft, CancellationToken ct = default);
    Task<IReadOnlyList<ParsedItemDraftRecord>> GetPendingAsync(string userId, CancellationToken ct = default);
    Task<ParsedItemDraftRecord?> GetByIdAsync(Guid parsedItemDraftId, string userId, CancellationToken ct = default);
    Task<ParsedItemDraftRecord?> UpdateAsync(Guid parsedItemDraftId, string userId, DraftUpdate update, CancellationToken ct = default);
    Task<bool> SetReviewStatusAsync(Guid parsedItemDraftId, string userId, string reviewStatus, CancellationToken ct = default);
}
