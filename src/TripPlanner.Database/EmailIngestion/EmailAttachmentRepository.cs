using Dapper;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.EmailIngestion;

/// <summary>Persisted metadata for a file delivered with a relayed message. Bytes are not returned by default.</summary>
public sealed record EmailAttachmentRecord(
    Guid AttachmentId,
    Guid InboxEmailId,
    string FileName,
    string ContentType,
    int SizeBytes,
    string? ExtractedText,
    DateTimeOffset CreatedAtUtc);

/// <summary>A new attachment to store against an already-inserted inbox email.</summary>
public sealed record NewEmailAttachment(
    Guid InboxEmailId,
    string FileName,
    string ContentType,
    byte[] Content,
    string? ExtractedText);

public interface IEmailAttachmentRepository
{
    Task<EmailAttachmentRecord> InsertAsync(NewEmailAttachment attachment, CancellationToken ct = default);
    Task<IReadOnlyList<EmailAttachmentRecord>> GetForEmailAsync(Guid inboxEmailId, CancellationToken ct = default);
}

public sealed class EmailAttachmentRepository : IEmailAttachmentRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public EmailAttachmentRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<EmailAttachmentRecord> InsertAsync(NewEmailAttachment attachment, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/InsertEmailAttachment.sql");
        return await conn.QuerySingleAsync<EmailAttachmentRecord>(new CommandDefinition(command, new
        {
            AttachmentId = Guid.NewGuid(),
            attachment.InboxEmailId,
            attachment.FileName,
            attachment.ContentType,
            SizeBytes = attachment.Content.Length,
            attachment.Content,
            attachment.ExtractedText
        }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<EmailAttachmentRecord>> GetForEmailAsync(Guid inboxEmailId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetEmailAttachments.sql");
        var rows = await conn.QueryAsync<EmailAttachmentRecord>(new CommandDefinition(query, new { InboxEmailId = inboxEmailId }, cancellationToken: ct));
        return rows.ToArray();
    }
}
