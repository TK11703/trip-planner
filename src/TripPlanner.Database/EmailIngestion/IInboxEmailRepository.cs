namespace TripPlanner.Database.EmailIngestion;

/// <summary>Persisted representation of a raw inbox email with full body content.</summary>
public sealed record InboxEmailRecord(
    Guid InboxEmailId,
    string UserId,
    string? MessageId,
    string Sender,
    string? Recipient,
    string Subject,
    string BodyText,
    string? BodyHtml,
    DateTimeOffset ReceivedAt,
    string DedupeHash,
    string ParseStatus,
    DateTimeOffset CreatedAtUtc);

/// <summary>List projection of an inbox email. Body columns are omitted so history reads stay cheap.</summary>
public sealed record InboxEmailSummary(
    Guid InboxEmailId,
    string UserId,
    string Sender,
    string Subject,
    DateTimeOffset ReceivedAt,
    string ParseStatus,
    DateTimeOffset CreatedAtUtc);

/// <summary>A new inbox email to store. The parse status is always terminal — there is no deferred state.</summary>
public sealed record NewInboxEmail(
    string UserId,
    string? MessageId,
    string Sender,
    string? Recipient,
    string Subject,
    string BodyText,
    string? BodyHtml,
    DateTimeOffset ReceivedAt,
    string DedupeHash,
    string ParseStatus);

public interface IInboxEmailRepository
{
    /// <summary>
    /// Inserts the email. Returns the persisted record, or null when the message was already
    /// ingested (same user_id + dedupe_hash). The unique-constraint collision is surfaced as a
    /// duplicate result rather than an exception, so concurrent relay retries are safe.
    /// </summary>
    Task<InboxEmailRecord?> InsertAsync(NewInboxEmail email, CancellationToken ct = default);

    Task<InboxEmailRecord?> GetByIdAsync(Guid inboxEmailId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<InboxEmailSummary>> GetListAsync(string userId, int limit, CancellationToken ct = default);
    Task UpdateParseStatusAsync(Guid inboxEmailId, string userId, string parseStatus, CancellationToken ct = default);
}

/// <summary>Terminal parse statuses persisted on <c>inbox_emails</c>.</summary>
public static class InboxEmailParseStatus
{
    public const string Parsed = "parsed";
    public const string Failed = "failed";
    public const string Unsupported = "unsupported";
}
