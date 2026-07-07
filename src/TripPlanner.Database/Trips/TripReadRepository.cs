using Dapper;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Database.Trips;

public interface ITripReadRepository
{
    Task<TripListResponse> GetPageAsync(string ownerUserId, string? callerEmail, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<TripSummary>> GetRecentAsync(string ownerUserId, int limit, CancellationToken cancellationToken);
    Task<TripDetail?> GetDetailAsync(string ownerUserId, Guid tripId, CancellationToken cancellationToken);
}

public sealed class TripReadRepository : ITripReadRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;
    public TripReadRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    { _factory = factory; _sql = sql; }

    public async Task<TripListResponse> GetPageAsync(string ownerUserId, string? callerEmail, int page, int pageSize, CancellationToken cancellationToken)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var query = _sql.Get("Queries/Trips/GetTripsPage.sql");
        var offset = (page - 1) * pageSize;
        var rows = (await conn.QueryAsync<TripSummaryPageRow>(new CommandDefinition(query, new { OwnerUserId = ownerUserId, CallerEmail = callerEmail, Limit = pageSize, Offset = offset }, cancellationToken: cancellationToken))).ToArray();
        var total = rows.FirstOrDefault()?.TotalCount ?? 0;
        var trips = rows.Select(r => new TripSummary(r.TripId, r.Name, r.StartDate, r.EndDate, r.UpdatedAtUtc, r.ItemCount, TripAccessLevels.Parse(r.AccessLevel), r.IsOwner)).ToArray();
        return new TripListResponse(trips, page, pageSize, total);
    }

    public async Task<IReadOnlyList<TripSummary>> GetRecentAsync(string ownerUserId, int limit, CancellationToken cancellationToken)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var query = _sql.Get("Queries/Trips/GetRecentTrips.sql");
        var rows = await conn.QueryAsync<TripSummaryRow>(new CommandDefinition(query, new { OwnerUserId = ownerUserId, Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(r => new TripSummary(r.TripId, r.Name, r.StartDate, r.EndDate, r.UpdatedAtUtc, r.ItemCount)).ToArray();
    }

    public async Task<TripDetail?> GetDetailAsync(string ownerUserId, Guid tripId, CancellationToken cancellationToken)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var query = _sql.Get("Queries/Trips/GetTripDetail.sql");
        var row = await conn.QuerySingleOrDefaultAsync<TripDetailRow>(new CommandDefinition(query, new { OwnerUserId = ownerUserId, TripId = tripId }, cancellationToken: cancellationToken));
        if (row is null) return null;
        // Legs and tracked items are loaded by the API endpoint via TripItemRepository; trip-only detail returns empty collections.
        return new TripDetail(row.TripId, row.Name, row.Description, row.StartDate, row.EndDate, row.CreatedAtUtc, row.UpdatedAtUtc,
            Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>());
    }

    private sealed record TripSummaryRow(Guid TripId, string Name, DateOnly StartDate, DateOnly EndDate, DateTimeOffset UpdatedAtUtc, int ItemCount);
    private sealed record TripSummaryPageRow(Guid TripId, string Name, DateOnly StartDate, DateOnly EndDate, DateTimeOffset UpdatedAtUtc, int ItemCount, string AccessLevel, bool IsOwner, int TotalCount);
    private sealed record TripDetailRow(Guid TripId, string Name, string? Description, DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
}
