using Dapper;
using Npgsql;
using TripPlanner.Database.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Database.Tests.TripItems;

/// <summary>
/// A database that predates this feature carries legs whose only clue about what they are is
/// whether someone filled in an origin. The migration has to read that clue the way the old UI did,
/// and it must not disturb any item already attached to a leg.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(MigratedLegacyPostgresCollection.Name)]
public class TripLegModeMigrationTests
{
    private readonly MigratedLegacyPostgresFixture _fixture;

    public TripLegModeMigrationTests(MigratedLegacyPostgresFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("blank-origin", "stay", null)]
    [InlineData("whitespace-origin", "stay", null)]
    [InlineData("origin-bearing", "travel", "car")]
    public async Task LegacyLegsAreClassifiedFromTheirOrigin(string title, string expectedKind, string? expectedMode)
    {
        await using var conn = await _fixture.OpenAsync();

        var kind = await conn.ExecuteScalarAsync<string>(
            "SELECT leg_kind FROM trip_legs WHERE title = @title", new { title });
        var mode = await conn.ExecuteScalarAsync<string?>(
            "SELECT transportation_mode FROM trip_legs WHERE title = @title", new { title });

        Assert.Equal(expectedKind, kind);
        Assert.Equal(expectedMode, mode);
    }

    /// <summary>
    /// A whitespace-only origin never meant the leg went anywhere, and it must not be promoted to
    /// travel — that would silently make a stay unable to hold items.
    /// </summary>
    [Fact]
    public async Task AWhitespaceOnlyOriginIsErasedRatherThanTreatedAsTravel()
    {
        await using var conn = await _fixture.OpenAsync();

        var origin = await conn.ExecuteScalarAsync<string?>(
            "SELECT origin FROM trip_legs WHERE title = 'whitespace-origin'");

        Assert.Null(origin);
    }

    /// <summary>
    /// A stay's destination only ever restated its title, so the migration clears it instead of
    /// leaving it to surface on the timeline of a leg the traveler may never reopen.
    /// </summary>
    [Theory]
    [InlineData("blank-origin")]
    [InlineData("whitespace-origin")]
    public async Task ALegacyStayLosesItsDestination(string title)
    {
        await using var conn = await _fixture.OpenAsync();

        var destination = await conn.ExecuteScalarAsync<string?>(
            "SELECT destination FROM trip_legs WHERE title = @title", new { title });

        Assert.Null(destination);
    }

    /// <summary>
    /// Car is chosen for every migrated travel leg precisely because it still accepts items, so no
    /// legacy row needs an exception and no item has to be moved.
    /// </summary>
    [Fact]
    public async Task ItemsOnMigratedLegsKeepTheirLeg()
    {
        await using var conn = await _fixture.OpenAsync();

        var pairs = (await conn.QueryAsync<(string ItemTitle, string LegTitle)>(
            """
            SELECT i.title, l.title
            FROM tracked_items i
            JOIN trip_legs l ON l.trip_leg_id = i.trip_leg_id
            ORDER BY i.title;
            """)).ToArray();

        Assert.Equal(
            new[] { ("item-on-origin-bearing", "origin-bearing"), ("item-on-stay", "blank-origin") },
            pairs);
    }

    [Fact]
    public async Task AnUnassignedItemStaysUnassigned()
    {
        await using var conn = await _fixture.OpenAsync();

        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tracked_items WHERE trip_leg_id IS NULL AND title = 'item-unassigned'"));
    }

    /// <summary>Booking details arrive empty; an unbooked leg is a normal state, not a gap to fill.</summary>
    [Fact]
    public async Task BookingDetailsStartOutEmpty()
    {
        await using var conn = await _fixture.OpenAsync();

        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM trip_legs WHERE travel_cost IS NOT NULL OR confirmation_code IS NOT NULL"));
    }

    [Fact]
    public async Task EveryMigratedLegHasAClassification()
    {
        await using var conn = await _fixture.OpenAsync();

        Assert.Equal(0, await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM trip_legs WHERE leg_kind IS NULL"));
    }

    /// <summary>
    /// The initializer re-runs every schema script on every start, so the migration must survive
    /// being applied a second time over its own output.
    /// </summary>
    [Fact]
    public async Task ReapplyingTheMigrationChangesNothing()
    {
        await using var conn = await _fixture.OpenAsync();
        var before = await ClassificationsAsync(conn);

        await conn.ExecuteAsync(_fixture.MigrationSql);

        Assert.Equal(before, await ClassificationsAsync(conn));
    }

    private static async Task<(string Title, string LegKind, string? Mode)[]> ClassificationsAsync(NpgsqlConnection conn) =>
        (await conn.QueryAsync<(string Title, string LegKind, string? Mode)>(
            "SELECT title, leg_kind, transportation_mode FROM trip_legs ORDER BY title")).ToArray();
}
