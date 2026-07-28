using Dapper;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.EmailIngestion;

public sealed class InboxEmailRepository : IInboxEmailRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public InboxEmailRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<InboxEmailRecord?> InsertAsync(NewInboxEmail email, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/InsertInboxEmail.sql");
        var row = await conn.QuerySingleOrDefaultAsync<InboxEmailRow>(new CommandDefinition(command, new
        {
            InboxEmailId = Guid.NewGuid(),
            email.UserId,
            email.Sender,
            email.Subject,
            email.BodyText,
            email.BodyHtml,
            email.ReceivedAt,
            email.DedupeHash
        }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<IReadOnlyList<InboxEmailRecord>> GetPendingAsync(int limit, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetPendingInboxEmails.sql");
        var rows = await conn.QueryAsync<InboxEmailRow>(new CommandDefinition(query, new { Limit = limit }, cancellationToken: ct));
        return rows.Select(r => r.ToRecord()).ToArray();
    }

    public async Task<IReadOnlyList<InboxEmailRecord>> GetListAsync(string userId, int limit, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetInboxEmails.sql");
        var rows = await conn.QueryAsync<InboxEmailRow>(new CommandDefinition(query, new { UserId = userId, Limit = limit }, cancellationToken: ct));
        return rows.Select(r => r.ToRecord()).ToArray();
    }

    public async Task UpdateParseStatusAsync(Guid inboxEmailId, string userId, string parseStatus, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/UpdateInboxEmailParseStatus.sql");
        await conn.ExecuteAsync(new CommandDefinition(command, new { InboxEmailId = inboxEmailId, UserId = userId, ParseStatus = parseStatus }, cancellationToken: ct));
    }

    private sealed record InboxEmailRow(
        Guid InboxEmailId,
        string UserId,
        string Sender,
        string Subject,
        string BodyText,
        string? BodyHtml,
        DateTimeOffset ReceivedAt,
        string DedupeHash,
        string ParseStatus,
        DateTimeOffset CreatedAtUtc)
    {
        public InboxEmailRecord ToRecord() => new(
            InboxEmailId, UserId, Sender, Subject, BodyText, BodyHtml,
            ReceivedAt, DedupeHash, ParseStatus, CreatedAtUtc);
    }
}
