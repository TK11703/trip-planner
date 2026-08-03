using Dapper;
using Npgsql;
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
        try
        {
            var row = await conn.QuerySingleOrDefaultAsync<InboxEmailRow>(new CommandDefinition(command, new
            {
                InboxEmailId = Guid.NewGuid(),
                email.UserId,
                email.MessageId,
                email.Sender,
                email.Recipient,
                email.Subject,
                email.BodyText,
                email.BodyHtml,
                email.ReceivedAt,
                email.DedupeHash,
                email.ParseStatus
            }, cancellationToken: ct));
            return row?.ToRecord();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // A concurrent relay retry won the race. Report it as a duplicate, not a failure.
            return null;
        }
    }

    public async Task<InboxEmailRecord?> GetByIdAsync(Guid inboxEmailId, string userId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetInboxEmailById.sql");
        var row = await conn.QuerySingleOrDefaultAsync<InboxEmailRow>(new CommandDefinition(query, new { InboxEmailId = inboxEmailId, UserId = userId }, cancellationToken: ct));
        return row?.ToRecord();
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
        string? MessageId,
        string Sender,
        string? Recipient,
        string Subject,
        string BodyText,
        string? BodyHtml,
        DateTimeOffset ReceivedAt,
        string DedupeHash,
        string ParseStatus,
        DateTimeOffset CreatedAtUtc)
    {
        public InboxEmailRecord ToRecord() => new(
            InboxEmailId, UserId, MessageId, Sender, Recipient, Subject, BodyText, BodyHtml,
            ReceivedAt, DedupeHash, ParseStatus, CreatedAtUtc);
    }
}
