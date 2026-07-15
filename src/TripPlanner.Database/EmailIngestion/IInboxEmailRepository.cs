using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Database.EmailIngestion;

/// <summary>Persisted representation of a raw inbox email with full body content.</summary>
public sealed record InboxEmailRecord(
    Guid InboxEmailId,
    string UserId,
    string Sender,
    string Subject,
    string BodyText,
    string? BodyHtml,
    DateTimeOffset ReceivedAt,
    string DedupeHash,
    string ParseStatus,
    DateTimeOffset CreatedAtUtc);

/// <summary>A new inbox email to store.</summary>
public sealed record NewInboxEmail(
    string UserId,
    string Sender,
    string Subject,
    string BodyText,
    string? BodyHtml,
    DateTimeOffset ReceivedAt,
    string DedupeHash);

public interface IInboxEmailRepository
{
    /// <summary>
    /// Inserts the email. Returns the persisted record, or null if a duplicate was detected
    /// (same user_id + dedupe_hash already exists).
    /// </summary>
    Task<InboxEmailRecord?> InsertAsync(NewInboxEmail email, CancellationToken ct = default);

    Task<IReadOnlyList<InboxEmailRecord>> GetPendingAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<InboxEmailRecord>> GetListAsync(string userId, int limit, CancellationToken ct = default);
    Task UpdateParseStatusAsync(Guid inboxEmailId, string userId, string parseStatus, CancellationToken ct = default);
}
