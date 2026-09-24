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
            draft.Confidence,
            draft.ProposedOutcome,
            draft.Origin,
            draft.Destination,
            draft.TransportationMode,
            draft.TravelCost,
            draft.TravelCostCurrency
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
            update.Notes,
            update.ProposedOutcome,
            update.Origin,
            update.Destination,
            update.TransportationMode,
            update.TravelCost
        }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<bool> SetReviewStatusAsync(
        Guid parsedItemDraftId,
        string userId,
        string reviewStatus,
        Guid? trackedItemId = null,
        string? proposedOutcome = null,
        Guid? createdTripLegId = null,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/UpdateParsedItemDraftReviewStatus.sql");
        var result = await conn.QuerySingleOrDefaultAsync(new CommandDefinition(command, new
        {
            ParsedItemDraftId = parsedItemDraftId,
            UserId = userId,
            ReviewStatus = reviewStatus,
            TrackedItemId = trackedItemId,
            ProposedOutcome = proposedOutcome,
            CreatedTripLegId = createdTripLegId
        }, cancellationToken: ct));
        return result is not null;
    }

    public async Task<ParsedItemDraftRecord?> MergeRecognitionAsync(
        Guid parsedItemDraftId,
        string userId,
        DraftRecognitionMerge merge,
        CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var command = _sql.Get("Commands/EmailIngestion/MergeParsedItemDraftRecognition.sql");
        var row = await conn.QuerySingleOrDefaultAsync<DraftRow>(new CommandDefinition(command, new
        {
            ParsedItemDraftId = parsedItemDraftId,
            UserId = userId,
            merge.ProposedOutcome,
            merge.Origin,
            merge.Destination,
            merge.TransportationMode,
            merge.TravelCost,
            merge.TravelCostCurrency,
            merge.Title,
            merge.Location,
            merge.ConfirmationCode,
            merge.StartLocal,
            merge.StartTimeZoneId,
            merge.TransportRecognitionState
        }, cancellationToken: ct));
        return row?.ToRecord();
    }

    public async Task<IReadOnlyList<PlacementCandidateLeg>> GetPlacementCandidateLegsAsync(string userId, string? callerEmail, CancellationToken ct = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/EmailIngestion/GetPlacementCandidateLegs.sql");
        var rows = await conn.QueryAsync<PlacementCandidateLeg>(new CommandDefinition(
            query, new { OwnerUserId = userId, CallerEmail = callerEmail }, cancellationToken: ct));
        return rows.ToArray();
    }

    /// <summary>
    /// Mapped by property rather than by constructor: Dapper reports a <c>text[]</c> column as
    /// <c>System.Array</c> when matching constructors, which no positional parameter can satisfy,
    /// so a parameterless shape is what lets <c>traveler_edited_fields</c> come back at all.
    /// </summary>
    private sealed class DraftRow
    {
        public Guid ParsedItemDraftId { get; init; }
        public Guid InboxEmailId { get; init; }
        public string UserId { get; init; } = string.Empty;
        public Guid? TripId { get; init; }
        public Guid? TripLegId { get; init; }
        public string? ItemType { get; init; }
        public string? Title { get; init; }
        public string? Location { get; init; }
        public DateTime? StartLocal { get; init; }
        public string? StartTimeZoneId { get; init; }
        public DateTime? EndLocal { get; init; }
        public string? EndTimeZoneId { get; init; }
        public string? ConfirmationCode { get; init; }
        public string? Notes { get; init; }
        public double Confidence { get; init; }
        public string ReviewStatus { get; init; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; init; }
        public Guid? TrackedItemId { get; init; }
        public string ProposedOutcome { get; init; } = DraftOutcomes.Item;
        public string? Origin { get; init; }
        public string? Destination { get; init; }
        public string? TransportationMode { get; init; }
        public decimal? TravelCost { get; init; }
        public string? TravelCostCurrency { get; init; }
        public Guid? CreatedTripLegId { get; init; }
        public string TransportRecognitionState { get; init; } = DraftRecognitionStates.Current;
        public string[]? TravelerEditedFields { get; init; }

        public ParsedItemDraftRecord ToRecord() => new(
            ParsedItemDraftId, InboxEmailId, UserId, TripId, TripLegId,
            ItemType, Title, Location,
            StartLocal, StartTimeZoneId, EndLocal, EndTimeZoneId,
            ConfirmationCode, Notes, Confidence, ReviewStatus, CreatedAtUtc, TrackedItemId,
            ProposedOutcome, Origin, Destination, TransportationMode,
            TravelCost, TravelCostCurrency, CreatedTripLegId, TransportRecognitionState,
            TravelerEditedFields ?? []);
    }
}
