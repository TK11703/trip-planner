namespace TripPlanner.Database.EmailIngestion;

/// <summary>Persisted representation of a parsed event draft.</summary>
public sealed record ParsedEventDraftRecord(
    Guid ParsedEventDraftId,
    Guid InboxEmailId,
    string UserId,
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
    string ReviewStatus,
    DateTimeOffset CreatedAtUtc);

/// <summary>A new parsed event draft to insert.</summary>
public sealed record NewParsedEventDraft(
    Guid InboxEmailId,
    string UserId,
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
    double Confidence);

/// <summary>Fields the user can edit on a pending draft.</summary>
public sealed record DraftUpdate(
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

public interface IParsedEventDraftRepository
{
    Task<ParsedEventDraftRecord?> InsertAsync(NewParsedEventDraft draft, CancellationToken ct = default);
    Task<IReadOnlyList<ParsedEventDraftRecord>> GetPendingAsync(string userId, CancellationToken ct = default);
    Task<ParsedEventDraftRecord?> GetByIdAsync(Guid parsedEventDraftId, string userId, CancellationToken ct = default);
    Task<ParsedEventDraftRecord?> UpdateAsync(Guid parsedEventDraftId, string userId, DraftUpdate update, CancellationToken ct = default);
    Task<bool> SetReviewStatusAsync(Guid parsedEventDraftId, string userId, string reviewStatus, CancellationToken ct = default);
}
