using System.Data.Common;
using Npgsql;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using TripPlanner.Database.Timeline;
using Xunit;

namespace TripPlanner.Database.Tests.Timeline;

/// <summary>
/// The timeline is the surface where a leg's classification decides whether the traveler is even
/// offered a place to drop an item, so the projection has to carry the classification out of the
/// migrated database intact.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(MigratedLegacyPostgresCollection.Name)]
public class TimelineQueryTests
{
    private readonly MigratedLegacyPostgresFixture _fixture;

    public TimelineQueryTests(MigratedLegacyPostgresFixture fixture) => _fixture = fixture;

    private Task<TimelineProjection> ProjectAsync(string ownerUserId = MigratedLegacyPostgresFixture.OwnerUserId) =>
        new TimelineRepository(new FixtureConnectionFactory(_fixture.ConnectionString), new SqlFileProvider())
            .GetTimelineAsync(ownerUserId, MigratedLegacyPostgresFixture.TripId, CancellationToken.None);

    [Fact]
    public async Task EveryLegArrivesClassified()
    {
        var projection = await ProjectAsync();

        Assert.All(projection.Legs, leg => Assert.False(string.IsNullOrWhiteSpace(leg.LegKind)));
    }

    /// <summary>
    /// The migration turned origin-bearing legs into car legs on purpose: car is the one travel mode
    /// that still holds items, so the timeline must keep offering that leg as a drop target.
    /// </summary>
    [Fact]
    public async Task AMigratedTravelLegIsACarLegAndStillAcceptsItems()
    {
        var projection = await ProjectAsync();

        var leg = projection.Legs.Single(l => l.TripLegId == MigratedLegacyPostgresFixture.OriginBearingLegId);

        Assert.Equal("travel", leg.LegKind);
        Assert.Equal("car", leg.TransportationMode);
        Assert.True(leg.CanContainItems);
        Assert.Equal("Car", leg.ModeLabel);
    }

    [Fact]
    public async Task AMigratedStayLegHasNoTransportationMode()
    {
        var projection = await ProjectAsync();

        var leg = projection.Legs.Single(l => l.TripLegId == MigratedLegacyPostgresFixture.StayLegId);

        Assert.Equal("stay", leg.LegKind);
        Assert.Null(leg.TransportationMode);
        Assert.True(leg.CanContainItems);
        Assert.Equal(string.Empty, leg.ModeLabel);
    }

    [Fact]
    public async Task ItemsStayWithTheLegTheyWereOn()
    {
        var projection = await ProjectAsync();

        Assert.Equal(
            new[] { "item-on-stay" },
            projection.Legs.Single(l => l.TripLegId == MigratedLegacyPostgresFixture.StayLegId).Items.Select(i => i.Title));
        Assert.Equal(
            new[] { "item-on-origin-bearing" },
            projection.Legs.Single(l => l.TripLegId == MigratedLegacyPostgresFixture.OriginBearingLegId).Items.Select(i => i.Title));
    }

    [Fact]
    public async Task AnUnassignedItemIsStillReported()
    {
        var projection = await ProjectAsync();

        Assert.Equal(new[] { "item-unassigned" }, projection.UnassignedItems.Select(i => i.Title));
    }

    /// <summary>
    /// Nothing was booked before the migration, so the leg-level cost stays empty while the item
    /// totals — which existed all along — come through untouched.
    /// </summary>
    [Fact]
    public async Task BookingDetailsAreAbsentWhileItemTotalsSurvive()
    {
        var projection = await ProjectAsync();

        Assert.All(projection.Legs, leg =>
        {
            Assert.Null(leg.TravelCost);
            Assert.Null(leg.ConfirmationCode);
        });

        Assert.Equal(25.00m, projection.Legs.Single(l => l.TripLegId == MigratedLegacyPostgresFixture.StayLegId).EstimatedCostTotal);
        Assert.Equal(40.00m, projection.Legs.Single(l => l.TripLegId == MigratedLegacyPostgresFixture.OriginBearingLegId).EstimatedCostTotal);
    }

    [Fact]
    public async Task Timeline_OnlyIncludesOwnerScopedItems()
    {
        var projection = await ProjectAsync("someone-else");

        Assert.Empty(projection.Legs);
        Assert.Empty(projection.UnassignedItems);
    }

    private sealed class FixtureConnectionFactory : IPostgresConnectionFactory
    {
        private readonly string _connectionString;
        public FixtureConnectionFactory(string connectionString) => _connectionString = connectionString;

        public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            return conn;
        }
    }
}
