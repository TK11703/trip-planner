using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using TripPlanner.Database.Trips;
using Xunit;

namespace TripPlanner.Database.Tests.Trips;

/// <summary>
/// Reads are scoped to the caller the same way writes are. A leak here would not throw or fail a
/// contract — it would quietly show one traveler another's itinerary — so the assertions are
/// about what is <em>absent</em> from a result as much as what is present.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class TripQueryOwnershipTests
{
    private readonly TripCommandRepository _commands;
    private readonly TripReadRepository _reads;

    public TripQueryOwnershipTests(PostgresFixture fixture)
    {
        var factory = new StubConnectionFactory(fixture.ConnectionString);
        var sql = new SqlFileProvider();
        _commands = new TripCommandRepository(factory, sql);
        _reads = new TripReadRepository(factory, sql);
    }

    private static string NewOwner() => $"owner-{Guid.NewGuid():N}";

    private Task<Guid> SeedTripAsync(string owner, string name) =>
        _commands.InsertAsync(
            owner,
            new CreateTripRequest(name, null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)),
            DateTimeOffset.UtcNow,
            default);

    [Fact]
    public async Task GetRecentTrips_FiltersByOwnerUserId()
    {
        var mine = NewOwner();
        var theirs = NewOwner();

        await SeedTripAsync(mine, "Mine A");
        await SeedTripAsync(mine, "Mine B");
        await SeedTripAsync(theirs, "Theirs");

        var page = await _reads.GetPageAsync(mine, callerEmail: null, page: 1, pageSize: 50, default);

        Assert.Equal(2, page.Trips.Count);
        Assert.All(page.Trips, t => Assert.StartsWith("Mine", t.Name, StringComparison.Ordinal));
        Assert.DoesNotContain(page.Trips, t => t.Name == "Theirs");
    }

    [Fact]
    public async Task TripSummaries_IncludeDescriptionCappedForCards()
    {
        var owner = NewOwner();
        var longDescription = new string('d', 800);
        await _commands.InsertAsync(
            owner,
            new CreateTripRequest("Described", longDescription, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)),
            DateTimeOffset.UtcNow,
            default);
        await SeedTripAsync(owner, "Undescribed");

        var page = await _reads.GetPageAsync(owner, callerEmail: null, page: 1, pageSize: 50, default);
        var recent = await _reads.GetRecentAsync(owner, 10, default);

        foreach (var trips in new[] { page.Trips, recent })
        {
            Assert.Equal(500, trips.Single(t => t.Name == "Described").Description!.Length);
            Assert.Null(trips.Single(t => t.Name == "Undescribed").Description);
        }
    }

    [Fact]
    public async Task GetTripDetail_ReturnsNullForOtherOwner()
    {
        var owner = NewOwner();
        var tripId = await SeedTripAsync(owner, "Private");

        Assert.NotNull(await _reads.GetDetailAsync(owner, tripId, default));
        Assert.Null(await _reads.GetDetailAsync(NewOwner(), tripId, default));
    }

    /// <summary>
    /// A trip nobody owns is not an error — it is simply not found. The distinction matters
    /// because the API turns both into the same "not found or denied" answer deliberately.
    /// </summary>
    [Fact]
    public async Task GetTripDetail_ReturnsNullForAnUnknownTrip()
    {
        Assert.Null(await _reads.GetDetailAsync(NewOwner(), Guid.NewGuid(), default));
    }
}
