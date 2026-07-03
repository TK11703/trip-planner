using Dapper;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Trips;

public interface ITripCommandRepository
{
    Task<Guid> InsertAsync(string ownerUserId, CreateTripRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<int> UpdateAsync(string ownerUserId, Guid tripId, UpdateTripRequest request, CancellationToken cancellationToken);
}

public sealed class TripCommandRepository : ITripCommandRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;
    public TripCommandRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    { _factory = factory; _sql = sql; }

    public async Task<Guid> InsertAsync(string ownerUserId, CreateTripRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = _sql.Get("Commands/Trips/InsertTrip.sql");
        var tripId = Guid.NewGuid();
        await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(cmd, new
        {
            TripId = tripId,
            OwnerUserId = ownerUserId,
            request.Name,
            request.Description,
            request.StartDate,
            request.EndDate,
            CreatedAtUtc = nowUtc
        }, cancellationToken: cancellationToken));
        return tripId;
    }

    public async Task<int> UpdateAsync(string ownerUserId, Guid tripId, UpdateTripRequest request, CancellationToken cancellationToken)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var cmd = _sql.Get("Commands/Trips/UpdateTrip.sql");
        return await conn.ExecuteAsync(new CommandDefinition(cmd, new
        {
            TripId = tripId,
            OwnerUserId = ownerUserId,
            request.Name,
            request.Description,
            request.StartDate,
            request.EndDate
        }, cancellationToken: cancellationToken));
    }
}
