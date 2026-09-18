using Dapper;
using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;
using TripPlanner.Database.Sql;
using Xunit;

namespace TripPlanner.Database.Tests.Infrastructure;

/// <summary>
/// A database seeded at the schema level that existed before feature 025, then advanced by the
/// migration under test. Sharing one container lets the migration tests and the timeline
/// projection tests observe the same migrated rows rather than a hand-written imitation of them.
/// </summary>
public sealed class MigratedLegacyPostgresFixture : IAsyncLifetime
{
    private const string MigrationScript = "014_trip_leg_modes.sql";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("tripplanner_migration_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private readonly SqlFileProvider _sql = new();

    public const string OwnerUserId = "legacy-user";
    public static readonly Guid TripId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid StayLegId = new("21111111-1111-1111-1111-111111111111");
    public static readonly Guid WhitespaceOriginLegId = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid OriginBearingLegId = new("23333333-3333-3333-3333-333333333333");

    public string ConnectionString => _container.GetConnectionString();

    public string MigrationSql => _sql.Get($"Schema/{MigrationScript}");

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var conn = await OpenAsync();

        foreach (var (name, body) in _sql.GetAllInDirectory("Schema"))
        {
            if (string.Equals(name, MigrationScript, StringComparison.OrdinalIgnoreCase)) continue;
            await conn.ExecuteAsync(body);
        }

        await SeedLegacyRowsAsync(conn);
        await conn.ExecuteAsync(MigrationSql);
    }

    private static Task SeedLegacyRowsAsync(NpgsqlConnection conn) => conn.ExecuteAsync(
        """
        INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
        VALUES ('11111111-1111-1111-1111-111111111111', 'legacy-user', 'Legacy trip',
                DATE '2026-07-10', DATE '2026-07-18');

        INSERT INTO trip_legs (
            trip_leg_id, trip_id, owner_user_id, title, origin, destination,
            start_at, end_at, start_local, start_time_zone_id, end_local, end_time_zone_id, sort_order)
        VALUES
            ('21111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'legacy-user',
             'blank-origin', NULL, 'Chicago',
             TIMESTAMPTZ '2026-07-11 08:00+00', TIMESTAMPTZ '2026-07-12 08:00+00',
             TIMESTAMP '2026-07-11 08:00', 'UTC', TIMESTAMP '2026-07-12 08:00', 'UTC', 0),
            ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'legacy-user',
             'whitespace-origin', '   ', 'Chicago',
             TIMESTAMPTZ '2026-07-12 08:00+00', TIMESTAMPTZ '2026-07-13 08:00+00',
             TIMESTAMP '2026-07-12 08:00', 'UTC', TIMESTAMP '2026-07-13 08:00', 'UTC', 1),
            ('23333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111', 'legacy-user',
             'origin-bearing', 'Paris', 'Chicago',
             TIMESTAMPTZ '2026-07-13 08:00+00', TIMESTAMPTZ '2026-07-14 08:00+00',
             TIMESTAMP '2026-07-13 08:00', 'UTC', TIMESTAMP '2026-07-14 08:00', 'UTC', 2);

        INSERT INTO tracked_items (
            trip_id, owner_user_id, trip_leg_id, item_type, title,
            starts_at, start_local, start_time_zone_id, display_color, sort_order, estimated_cost)
        VALUES
            ('11111111-1111-1111-1111-111111111111', 'legacy-user', '21111111-1111-1111-1111-111111111111',
             'activity', 'item-on-stay',
             TIMESTAMPTZ '2026-07-11 12:00+00', TIMESTAMP '2026-07-11 12:00', 'UTC', 'slate', 0, 25.00),
            ('11111111-1111-1111-1111-111111111111', 'legacy-user', '23333333-3333-3333-3333-333333333333',
             'activity', 'item-on-origin-bearing',
             TIMESTAMPTZ '2026-07-13 12:00+00', TIMESTAMP '2026-07-13 12:00', 'UTC', 'slate', 0, 40.00),
            ('11111111-1111-1111-1111-111111111111', 'legacy-user', NULL,
             'activity', 'item-unassigned',
             TIMESTAMPTZ '2026-07-15 12:00+00', TIMESTAMP '2026-07-15 12:00', 'UTC', 'slate', 0, NULL);
        """);

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class MigratedLegacyPostgresCollection : ICollectionFixture<MigratedLegacyPostgresFixture>
{
    public const string Name = "MigratedLegacyPostgres";
}
