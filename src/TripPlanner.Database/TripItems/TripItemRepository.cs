using Dapper;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.TripItems;

public interface ITripItemRepository
{
    Task<IReadOnlyList<TripLegDto>> GetLegsAsync(string ownerUserId, Guid tripId, CancellationToken ct);
    Task<IReadOnlyList<TrackedItemDto>> GetTrackedItemsAsync(string ownerUserId, Guid tripId, CancellationToken ct);
    Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(string ownerUserId, Guid tripId, CancellationToken ct);

    Task<Guid?> CreateLegAsync(string ownerUserId, Guid tripId, CreateTripLegRequest request, DateTimeOffset nowUtc, CancellationToken ct);
    Task<int> UpdateLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct);
    Task<int> DeleteLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct);

    Task<Guid?> CreateTrackedItemAsync(string ownerUserId, Guid tripId, CreateTrackedItemRequest request, DateTimeOffset nowUtc, CancellationToken ct);
    Task<int> UpdateTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct);
    Task<int> DeleteTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, CancellationToken ct);
    Task<int> CountItemsForLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct);
}

public sealed class TripItemRepository : ITripItemRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;
    public TripItemRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql) { _factory = factory; _sql = sql; }

    private string LegSql(string section) => SqlSections.Extract(_sql.Get("Commands/TripLegs/UpsertAndDeleteTripLegs.sql"), section);
    private string ItemSql(string section) => SqlSections.Extract(_sql.Get("Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql"), section);

    public async Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(string ownerUserId, Guid tripId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/TripLegs/GetTripLegDefaults.sql");
        return await conn.QuerySingleOrDefaultAsync<TripLegDefaultsResponse>(new CommandDefinition(query, new { OwnerUserId = ownerUserId, TripId = tripId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TripLegDto>> GetLegsAsync(string ownerUserId, Guid tripId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<TripLegDto>(new CommandDefinition(LegSql("SelectByTrip"), new { OwnerUserId = ownerUserId, TripId = tripId }, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<IReadOnlyList<TrackedItemDto>> GetTrackedItemsAsync(string ownerUserId, Guid tripId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<TrackedItemDto>(new CommandDefinition(ItemSql("SelectByTrip"), new { OwnerUserId = ownerUserId, TripId = tripId }, cancellationToken: ct));
        return rows.ToArray();
    }

    public async Task<Guid?> CreateLegAsync(string ownerUserId, Guid tripId, CreateTripLegRequest request, DateTimeOffset nowUtc, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var id = Guid.NewGuid();
        var rows = await conn.ExecuteAsync(new CommandDefinition(LegSql("Insert"), new
        {
            TripLegId = id, TripId = tripId, OwnerUserId = ownerUserId,
            request.Title, request.Origin, request.Destination,
            request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId,
            StartAt = ToInstant(request.StartLocal, request.StartTimeZoneId),
            EndAt = ToInstant(request.EndLocal, request.EndTimeZoneId),
            request.Notes,
            SortOrder = 0, NowUtc = nowUtc
        }, cancellationToken: ct));
        return rows == 0 ? null : id;
    }

    public async Task<int> UpdateLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(LegSql("Update"), new
        {
            TripLegId = tripLegId, TripId = tripId, OwnerUserId = ownerUserId,
            request.Title, request.Origin, request.Destination,
            request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId,
            StartAt = ToInstant(request.StartLocal, request.StartTimeZoneId),
            EndAt = ToInstant(request.EndLocal, request.EndTimeZoneId),
            request.Notes,
            SortOrder = 0
        }, cancellationToken: ct));
    }

    public async Task<int> DeleteLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(LegSql("Delete"), new { TripLegId = tripLegId, TripId = tripId, OwnerUserId = ownerUserId }, cancellationToken: ct));
    }

    public async Task<Guid?> CreateTrackedItemAsync(string ownerUserId, Guid tripId, CreateTrackedItemRequest request, DateTimeOffset nowUtc, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var id = Guid.NewGuid();
        var rows = await conn.ExecuteAsync(new CommandDefinition(ItemSql("Insert"), new
        {
            TrackedItemId = id, TripId = tripId, TripLegId = request.TripLegId, OwnerUserId = ownerUserId,
            request.ItemType, request.Title, request.Location,
            request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId,
            StartsAt = ToInstant(request.StartLocal, request.StartTimeZoneId),
            EndsAt = request.EndLocal is { } endLocal
                ? ToInstant(endLocal, request.EndTimeZoneId ?? request.StartTimeZoneId)
                : (DateTimeOffset?)null,
            DisplayColor = TrackedItemColors.Normalize(request.DisplayColor),
            request.ConfirmationCode, request.Notes, request.EstimatedCost,
            SortOrder = 0, NowUtc = nowUtc
        }, cancellationToken: ct));
        return rows == 0 ? null : id;
    }

    public async Task<int> UpdateTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(ItemSql("Update"), new
        {
            TrackedItemId = trackedItemId, TripId = tripId, TripLegId = request.TripLegId, OwnerUserId = ownerUserId,
            request.ItemType, request.Title, request.Location,
            request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId,
            StartsAt = ToInstant(request.StartLocal, request.StartTimeZoneId),
            EndsAt = request.EndLocal is { } endLocal
                ? ToInstant(endLocal, request.EndTimeZoneId ?? request.StartTimeZoneId)
                : (DateTimeOffset?)null,
            DisplayColor = TrackedItemColors.Normalize(request.DisplayColor),
            request.ConfirmationCode, request.Notes, request.EstimatedCost,
            SortOrder = 0
        }, cancellationToken: ct));
    }

    public async Task<int> DeleteTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(ItemSql("Delete"), new { TrackedItemId = trackedItemId, TripId = tripId, OwnerUserId = ownerUserId }, cancellationToken: ct));
    }

    public async Task<int> CountItemsForLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(ItemSql("CountByLeg"), new { OwnerUserId = ownerUserId, TripId = tripId, TripLegId = tripLegId }, cancellationToken: ct));
    }

    private static DateTimeOffset ToInstant(DateTime local, string timeZoneId)
    {
        var timeZone = TimezoneOptions.FindTimeZone(timeZoneId) ?? TimeZoneInfo.Utc;
        var unspecifiedLocal = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(unspecifiedLocal);
        return new DateTimeOffset(unspecifiedLocal, offset).ToUniversalTime();
    }
}

internal static class SqlSections
{
    /// <summary>
    /// Extracts a named SQL section. Sections are delimited by lines starting with
    /// <c>-- @Name</c>. Returns text from the named marker to the next marker or EOF.
    /// </summary>
    public static string Extract(string source, string sectionName)
    {
        var marker = "-- @" + sectionName;
        var startIdx = source.IndexOf(marker, StringComparison.Ordinal);
        if (startIdx < 0) throw new InvalidOperationException($"SQL section '{sectionName}' not found.");
        startIdx = source.IndexOf('\n', startIdx) + 1;
        var nextIdx = source.IndexOf("\n-- @", startIdx, StringComparison.Ordinal);
        var body = nextIdx < 0 ? source[startIdx..] : source[startIdx..nextIdx];
        return body.Trim();
    }
}
