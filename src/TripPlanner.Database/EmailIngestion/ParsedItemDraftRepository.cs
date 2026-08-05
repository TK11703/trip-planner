using Dapper;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.EmailIngestion;

public sealed class ParsedItemDraftRepository : IParsedItemDraftRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public ParsedItemDraftRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<ParsedItemDraftRecord?> InsertAsync(NewParsedItemDraft draft, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/InsertParsedItemDraft.sql");
        var row = await conn.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition(command, new
        {
            ParsedItemDraftId = Guid.NewGuid(),
            draft.InboxEmailId,
            draft.UserId,
            draft.TripId,
            draft.TripLegId,
            draft.ItemType,
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

    public async Task<IReadOnlyList<ParsedItemDraftRecord>> GetPendingAsync(string userId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetParsedItemDrafts.sql");
        var rows = await conn.QueryAsync<DraftRow>(new CommandDefinition(query, new { UserId = userId }, cancellationToken: ct));
        return rows.Select(r => r.ToRecord()).ToArray();
    }

    public async Task<ParsedItemDraftRecord?> GetByIdAsync(Guid parsedItemDraftId, string userId, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetParsedItemDraftById.sql");
        var row = await conn.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition(query, new { ParsedItemDraftId = parsedItemDraftId, UserId = userId }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<ParsedItemDraftRecord?> UpdateAsync(Guid parsedItemDraftId, string userId, DraftUpdate update, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/UpdateParsedItemDraft.sql");
        var row = await conn.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition(command, new
        {
            ParsedItemDraftId = parsedItemDraftId,
            UserId = userId,
            update.TripId,
            update.TripLegId,
            update.ItemType,
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

    public async Task<bool> SetReviewStatusAsync(Guid parsedItemDraftId, string userId, string reviewStatus, Guid? trackedItemId = null, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/UpdateParsedItemDraftReviewStatus.sql");
        var result = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(command, new
        {
            ParsedItemDraftId = parsedItemDraftId,
            UserId = userId,
            ReviewStatus = reviewStatus,
            TrackedItemId = trackedItemId
        }, cancellationToken: ct));
        return result is not null;
    }

    public async Task<IReadOnlyList<PlacementCandidateLeg>> GetPlacementCandidateLegsAsync(string userId, string? callerEmail, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetPlacementCandidateLegs.sql");
        var rows = await conn.QueryAsync<PlacementCandidateLeg>(new CommandDefinition(
            query, new { OwnerUserId = userId, CallerEmail = callerEmail }, cancellationToken: ct));
        return rows.ToArray();
    }

    private sealed record DraftRow(
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
        Guid? TrackedItemId)
    {
        public ParsedItemDraftRecord ToRecord() => new(
            ParsedItemDraftId, InboxEmailId, UserId, TripId, TripLegId,
            ItemType, Title, Location,
            StartLocal, StartTimeZoneId, EndLocal, EndTimeZoneId,
            ConfirmationCode, Notes, Confidence, ReviewStatus, CreatedAtUtc, TrackedItemId);
    }
}
