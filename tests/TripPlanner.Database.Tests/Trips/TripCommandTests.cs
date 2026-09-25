using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using TripPlanner.Database.Trips;
using Xunit;

namespace TripPlanner.Database.Tests.Trips;

/// <summary>
/// Ownership is carried in the WHERE clause of every write, not in a service check above it.
/// That only means anything if the SQL actually says so, which is why these run against a real
/// database rather than a fake repository.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class TripCommandTests
{
    private readonly TripCommandRepository _commands;
    private readonly TripReadRepository _reads;

    public TripCommandTests(PostgresFixture fixture)
    {
        var factory = new StubConnectionFactory(fixture.ConnectionString);
        var sql = new SqlFileProvider();
        _commands = new TripCommandRepository(factory, sql);
        _reads = new TripReadRepository(factory, sql);
    }

    private static string NewOwner() => $"owner-{Guid.NewGuid():N}";

    private static CreateTripRequest NewTrip(string name = "Trip") =>
        new(name, "Description", new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20));

    [Fact]
    public async Task InsertTrip_PersistsOwnerUserId()
    {
        var owner = NewOwner();

        var tripId = await _commands.InsertAsync(owner, NewTrip("Hawaii"), DateTimeOffset.UtcNow, default);

        var detail = await _reads.GetDetailAsync(owner, tripId, default);
        Assert.NotNull(detail);
        Assert.Equal("Hawaii", detail!.Name);

        // The same row is invisible to anyone else, which is the actual claim.
        Assert.Null(await _reads.GetDetailAsync(NewOwner(), tripId, default));
    }

    [Fact]
    public async Task UpdateTrip_RejectsCrossOwner()
    {
        var owner = NewOwner();
        var tripId = await _commands.InsertAsync(owner, NewTrip("Mine"), DateTimeOffset.UtcNow, default);

        var affected = await _commands.UpdateAsync(
            NewOwner(), tripId,
            new UpdateTripRequest("Hijacked", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)),
            default);

        // No rows matched, and nothing changed for the real owner.
        Assert.Equal(0, affected);
        var detail = await _reads.GetDetailAsync(owner, tripId, default);
        Assert.Equal("Mine", detail!.Name);
        Assert.Equal(new DateOnly(2026, 7, 10), detail.StartDate);
    }

    [Fact]
    public async Task UpdateTrip_AppliesToTheOwnersOwnTrip()
    {
        var owner = NewOwner();
        var tripId = await _commands.InsertAsync(owner, NewTrip("Before"), DateTimeOffset.UtcNow, default);

        var affected = await _commands.UpdateAsync(
            owner, tripId,
            new UpdateTripRequest("After", "Changed", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 9)),
            default);

        Assert.Equal(1, affected);
        var detail = await _reads.GetDetailAsync(owner, tripId, default);
        Assert.Equal("After", detail!.Name);
        Assert.Equal(new DateOnly(2026, 8, 1), detail.StartDate);
    }

    [Fact]
    public async Task DeleteTrip_RejectsCrossOwner()
    {
        var owner = NewOwner();
        var tripId = await _commands.InsertAsync(owner, NewTrip("Keep"), DateTimeOffset.UtcNow, default);

        Assert.Equal(0, await _commands.DeleteAsync(NewOwner(), tripId, default));
        Assert.NotNull(await _reads.GetDetailAsync(owner, tripId, default));

        Assert.Equal(1, await _commands.DeleteAsync(owner, tripId, default));
        Assert.Null(await _reads.GetDetailAsync(owner, tripId, default));
    }
}
