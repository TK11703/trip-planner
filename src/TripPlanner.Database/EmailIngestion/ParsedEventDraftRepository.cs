using Dapper;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.EmailIngestion;

public sealed class ParsedEventDraftRepository : IParsedEventDraftRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public ParsedEventDraftRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<ParsedEventDraftRecord?> InsertAsync(NewParsedEventDraft draft, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/InsertParsedEventDraft.sql");
        var row = await conn.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition(command, new
        {
            ParsedEventDraftId = Guid.NewGuid(),
            draft.InboxEmailId,
            draft.UserId,
            draft.TripId,
            draft.TripLegId,
            draft.EventType,
            draft.Title,
            draft.Location,
            draft.StartLocal,
            draft.StartTimeZoneId,
            draft.EndLocal,
            draft.EndTimeZoneId,
            draft.ConfirmationCode,
            draft.Notes,
            draft.Confidence
        }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<IReadOnlyList<ParsedEventDraftRecord>> GetPendingAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetParsedEventDrafts.sql");
        var rows = await conn.QueryAsync<DraftRow>(new CommandDefinition(query, new { UserId = userId }, cancellationToken: ct));
        return rows.Select(r => r.ToRecord()).ToArray();
    }

    public async Task<ParsedEventDraftRecord?> GetByIdAsync(Guid parsedEventDraftId, string userId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetParsedEventDraftById.sql");
        var row = await conn.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition(query, new { ParsedEventDraftId = parsedEventDraftId, UserId = userId }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<ParsedEventDraftRecord?> UpdateAsync(Guid parsedEventDraftId, string userId, DraftUpdate update, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/UpdateParsedEventDraft.sql");
        var row = await conn.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition(command, new
        {
            ParsedEventDraftId = parsedEventDraftId,
            UserId = userId,
            update.TripId,
            update.TripLegId,
            update.EventType,
            update.Title,
            update.Location,
            update.StartLocal,
            update.StartTimeZoneId,
            update.EndLocal,
            update.EndTimeZoneId,
            update.ConfirmationCode,
            update.Notes
        }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<bool> SetReviewStatusAsync(Guid parsedEventDraftId, string userId, string reviewStatus, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/UpdateParsedEventDraftReviewStatus.sql");
        var result = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(command, new
        {
            ParsedEventDraftId = parsedEventDraftId,
            UserId = userId,
            ReviewStatus = reviewStatus
        }, cancellationToken: ct));
        return result is not null;
    }

    private sealed record DraftRow(
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
        DateTimeOffset CreatedAtUtc)
    {
        public ParsedEventDraftRecord ToRecord() => new(
            ParsedEventDraftId, InboxEmailId, UserId, TripId, TripLegId,
            EventType, Title, Location,
            StartLocal, StartTimeZoneId, EndLocal, EndTimeZoneId,
            ConfirmationCode, Notes, Confidence, ReviewStatus, CreatedAtUtc);
    }
}
