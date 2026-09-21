using Dapper;
using Npgsql;
using TripPlanner.Database.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Database.Tests.TripItems;

/// <summary>
/// The rule "items only live where the traveler controls the stop" is enforced by the database, not
/// just the API, because two well-formed requests can arrive at the same instant: one adding an
/// item to a car leg, one turning that leg into a flight. Either order must leave the trip
/// consistent, so one of the two has to lose.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
public class TripLegModeInvariantTests : IClassFixture<PostgresFixture>
{
    private const string ConstraintName = "trip_legs_item_eligibility";

    private readonly PostgresFixture _fixture;

    public TripLegModeInvariantTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task<(Guid TripId, Guid LegId)> SeedAsync(
        NpgsqlConnection conn, string legKind, string? mode, NpgsqlTransaction? tx = null)
    {
        var owner = $"user-{Guid.NewGuid():N}";
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();

        await conn.ExecuteAsync(
            """
            INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
            VALUES (@tripId, @owner, 'Trip', DATE '2026-07-10', DATE '2026-07-18');

            INSERT INTO trip_legs (
                trip_leg_id, trip_id, owner_user_id, title, origin, destination,
                start_at, end_at, start_local, start_time_zone_id, end_local, end_time_zone_id,
                sort_order, leg_kind, transportation_mode)
            VALUES (
                @legId, @tripId, @owner, 'Leg',
                CASE WHEN @legKind = 'travel' THEN 'Paris' END,
                CASE WHEN @legKind = 'travel' THEN 'Chicago' END,
                TIMESTAMPTZ '2026-07-11 08:00+00', TIMESTAMPTZ '2026-07-12 08:00+00',
                TIMESTAMP '2026-07-11 08:00', 'UTC', TIMESTAMP '2026-07-12 08:00', 'UTC',
                0, @legKind, @mode);
            """,
            new { tripId, legId, owner, legKind, mode }, tx);

        return (tripId, legId);
    }

    private static Task<int> AddItemAsync(NpgsqlConnection conn, Guid tripId, Guid legId, NpgsqlTransaction? tx = null) =>
        conn.ExecuteAsync(
            """
            INSERT INTO tracked_items (
                tracked_item_id, trip_id, owner_user_id, trip_leg_id, item_type, title,
                starts_at, start_local, start_time_zone_id, display_color, sort_order)
            SELECT gen_random_uuid(), @tripId, t.owner_user_id, @legId, 'activity', 'Museum',
                   TIMESTAMPTZ '2026-07-11 12:00+00', TIMESTAMP '2026-07-11 12:00', 'UTC', 'slate', 0
            FROM trips t WHERE t.trip_id = @tripId;
            """,
            new { tripId, legId }, tx);

    private static Task<int> SetModeAsync(NpgsqlConnection conn, Guid legId, string mode, NpgsqlTransaction? tx = null) =>
        conn.ExecuteAsync(
            "UPDATE trip_legs SET leg_kind = 'travel', transportation_mode = @mode WHERE trip_leg_id = @legId;",
            new { legId, mode }, tx);

    public static TheoryData<string> RestrictedModes => new() { "flight", "train", "bus", "boat" };

    [Theory]
    [MemberData(nameof(RestrictedModes))]
    public async Task AnItemCannotBeInsertedOntoARestrictedLeg(string mode)
    {
        await using var conn = await OpenAsync();
        var (tripId, legId) = await SeedAsync(conn, "travel", mode);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AddItemAsync(conn, tripId, legId));

        Assert.Equal(ConstraintName, ex.ConstraintName);
        Assert.Equal(0, await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tracked_items WHERE trip_leg_id = @legId", new { legId }));
    }

    [Theory]
    [InlineData("travel", "car")]
    [InlineData("stay", null)]
    public async Task AnItemIsAcceptedOnAnEligibleLeg(string legKind, string? mode)
    {
        await using var conn = await OpenAsync();
        var (tripId, legId) = await SeedAsync(conn, legKind, mode);

        await AddItemAsync(conn, tripId, legId);

        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tracked_items WHERE trip_leg_id = @legId", new { legId }));
    }

    [Theory]
    [MemberData(nameof(RestrictedModes))]
    public async Task APopulatedLegCannotBecomeRestricted(string mode)
    {
        await using var conn = await OpenAsync();
        var (tripId, legId) = await SeedAsync(conn, "travel", "car");
        await AddItemAsync(conn, tripId, legId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => SetModeAsync(conn, legId, mode));

        Assert.Equal(ConstraintName, ex.ConstraintName);
        Assert.Equal("car", await conn.ExecuteScalarAsync<string>(
            "SELECT transportation_mode FROM trip_legs WHERE trip_leg_id = @legId", new { legId }));
    }

    [Theory]
    [MemberData(nameof(RestrictedModes))]
    public async Task AnEmptyLegMayBecomeRestricted(string mode)
    {
        await using var conn = await OpenAsync();
        var (_, legId) = await SeedAsync(conn, "travel", "car");

        await SetModeAsync(conn, legId, mode);

        Assert.Equal(mode, await conn.ExecuteScalarAsync<string>(
            "SELECT transportation_mode FROM trip_legs WHERE trip_leg_id = @legId", new { legId }));
    }

    /// <summary>
    /// The race, run for real: an open transaction holds an item against a car leg while another
    /// connection tries to turn that leg into a flight. The second must block on the item
    /// transaction's row lock and then fail once the item is visible.
    /// </summary>
    [Fact]
    public async Task AnItemBeingAddedBlocksAConcurrentSwitchToARestrictedMode()
    {
        await using var setup = await OpenAsync();
        var (tripId, legId) = await SeedAsync(setup, "travel", "car");

        await using var adder = await OpenAsync();
        await using var switcher = await OpenAsync();

        await using var addTx = await adder.BeginTransactionAsync();
        await AddItemAsync(adder, tripId, legId, addTx);

        // The switch cannot proceed while the item transaction holds the leg row.
        var switchTask = Task.Run(async () =>
        {
            await using var switchTx = await switcher.BeginTransactionAsync();
            await SetModeAsync(switcher, legId, "flight", switchTx);
            await switchTx.CommitAsync();
        });

        var finishedEarly = await Task.WhenAny(switchTask, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(switchTask, finishedEarly);

        await addTx.CommitAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => switchTask);
        Assert.Equal(ConstraintName, ex.ConstraintName);

        // The item survived and the leg is still a car leg, so the trip stayed consistent.
        Assert.Equal(1, await setup.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tracked_items WHERE trip_leg_id = @legId", new { legId }));
        Assert.Equal("car", await setup.ExecuteScalarAsync<string>(
            "SELECT transportation_mode FROM trip_legs WHERE trip_leg_id = @legId", new { legId }));
    }

    /// <summary>The mirror image: when the mode change commits first, the item is the one refused.</summary>
    [Fact]
    public async Task ASwitchToARestrictedModeBlocksAConcurrentItemAssignment()
    {
        await using var setup = await OpenAsync();
        var (tripId, legId) = await SeedAsync(setup, "travel", "car");

        await using var switcher = await OpenAsync();
        await using var adder = await OpenAsync();

        await using var switchTx = await switcher.BeginTransactionAsync();
        await SetModeAsync(switcher, legId, "flight", switchTx);

        var addTask = Task.Run(async () =>
        {
            await using var addTx = await adder.BeginTransactionAsync();
            await AddItemAsync(adder, tripId, legId, addTx);
            await addTx.CommitAsync();
        });

        var finishedEarly = await Task.WhenAny(addTask, Task.Delay(TimeSpan.FromSeconds(1)));
        Assert.NotSame(addTask, finishedEarly);

        await switchTx.CommitAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => addTask);
        Assert.Equal(ConstraintName, ex.ConstraintName);

        Assert.Equal(0, await setup.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tracked_items WHERE trip_leg_id = @legId", new { legId }));
        Assert.Equal("flight", await setup.ExecuteScalarAsync<string>(
            "SELECT transportation_mode FROM trip_legs WHERE trip_leg_id = @legId", new { legId }));
    }

    /// <summary>Moving an existing item onto a restricted leg is the same violation as creating one there.</summary>
    [Fact]
    public async Task AnItemCannotBeMovedOntoARestrictedLeg()
    {
        await using var conn = await OpenAsync();
        var (tripId, carLegId) = await SeedAsync(conn, "travel", "car");
        await AddItemAsync(conn, tripId, carLegId);

        var flightLegId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO trip_legs (
                trip_id, owner_user_id, title, origin, destination,
                start_at, end_at, start_local, start_time_zone_id, end_local, end_time_zone_id,
                sort_order, leg_kind, transportation_mode)
            SELECT @tripId, t.owner_user_id, 'Flight', 'Paris', 'Chicago',
                   TIMESTAMPTZ '2026-07-13 08:00+00', TIMESTAMPTZ '2026-07-13 18:00+00',
                   TIMESTAMP '2026-07-13 08:00', 'UTC', TIMESTAMP '2026-07-13 18:00', 'UTC',
                   1, 'travel', 'flight'
            FROM trips t WHERE t.trip_id = @tripId
            RETURNING trip_leg_id;
            """,
            new { tripId });

        var ex = await Assert.ThrowsAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE tracked_items SET trip_leg_id = @flightLegId WHERE trip_leg_id = @carLegId",
            new { flightLegId, carLegId }));

        Assert.Equal(ConstraintName, ex.ConstraintName);
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tracked_items WHERE trip_leg_id = @carLegId", new { carLegId }));
    }

    /// <summary>An unassigned item is nobody's passenger, so the trigger has nothing to say about it.</summary>
    [Fact]
    public async Task AnUnassignedItemIsAlwaysAccepted()
    {
        await using var conn = await OpenAsync();
        var (tripId, _) = await SeedAsync(conn, "travel", "flight");

        await conn.ExecuteAsync(
            """
            INSERT INTO tracked_items (
                trip_id, owner_user_id, trip_leg_id, item_type, title,
                starts_at, start_local, start_time_zone_id, display_color, sort_order)
            SELECT @tripId, t.owner_user_id, NULL, 'activity', 'Museum',
                   TIMESTAMPTZ '2026-07-11 12:00+00', TIMESTAMP '2026-07-11 12:00', 'UTC', 'slate', 0
            FROM trips t WHERE t.trip_id = @tripId;
            """,
            new { tripId });

        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM tracked_items WHERE trip_id = @tripId AND trip_leg_id IS NULL", new { tripId }));
    }
}
