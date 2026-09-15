using Dapper;
using Npgsql;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Initialization;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;

namespace TripPlanner.Database.Tests.Initialization;

public sealed class DatabaseInitializerTests(MigrationFixture fixture) : IClassFixture<MigrationFixture>
{
    private const string CreateDemoTable = "CREATE TABLE mig_demo (id int PRIMARY KEY);";
    private const string CreateOtherTable = "CREATE TABLE mig_other (id int PRIMARY KEY);";

    private static IServiceProvider BuildServices(string connectionString, ISqlFileProvider sql) =>
        new StubServiceProvider(
            (typeof(IPostgresConnectionFactory), new StubConnectionFactory(connectionString)),
            (typeof(ISqlFileProvider), sql));

    [Fact]
    public async Task AppliesEachMigrationExactlyOnce()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        // Neither script is idempotent, so a second application would throw "already exists".
        var sql = new StubSqlFileProvider()
            .AddSchemaScript("001_demo.sql", CreateDemoTable)
            .AddSchemaScript("002_other.sql", CreateOtherTable);
        var services = BuildServices(connectionString, sql);

        await new DatabaseInitializer().InitializeAsync(services, "release-1");
        await new DatabaseInitializer().InitializeAsync(services, "release-2");

        var ids = await QueryAsync<string>(connectionString, "SELECT migration_id FROM schema_migrations ORDER BY migration_id");
        Assert.Equal(["001_demo.sql", "002_other.sql"], ids);
    }

    [Fact]
    public async Task RecordsLedgerEntryForEachAppliedMigration()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var sql = new StubSqlFileProvider().AddSchemaScript("001_demo.sql", CreateDemoTable);
        var services = BuildServices(connectionString, sql);

        await new DatabaseInitializer().InitializeAsync(services, "release-42");

        await using var connection = new NpgsqlConnection(connectionString);
        var row = await connection.QuerySingleAsync<LedgerRow>(
            "SELECT checksum, release_id, duration_milliseconds FROM schema_migrations WHERE migration_id = '001_demo.sql'");

        Assert.Equal(MigrationLedger.ComputeChecksum(CreateDemoTable), row.checksum);
        Assert.Equal("release-42", row.release_id);
        Assert.True(row.duration_milliseconds >= 0);
    }

    [Fact]
    public async Task EditingAnAppliedMigrationFailsFast()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var sql = new StubSqlFileProvider().AddSchemaScript("001_demo.sql", CreateDemoTable);
        var services = BuildServices(connectionString, sql);
        await new DatabaseInitializer().InitializeAsync(services, "release-1");

        sql.ReplaceSchemaScript("001_demo.sql", CreateDemoTable + " -- edited");

        await Assert.ThrowsAsync<DatabaseInitializer.MigrationChecksumMismatchException>(
            () => new DatabaseInitializer().InitializeAsync(services, "release-2"));
    }

    [Fact]
    public async Task FailedMigrationLeavesNoLedgerEntry()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var sql = new StubSqlFileProvider()
            .AddSchemaScript("001_demo.sql", CreateDemoTable)
            .AddSchemaScript("002_broken.sql", "THIS IS NOT SQL;");
        var services = BuildServices(connectionString, sql);

        await Assert.ThrowsAnyAsync<Exception>(() => new DatabaseInitializer().InitializeAsync(services, "release-1"));

        var ids = await QueryAsync<string>(connectionString, "SELECT migration_id FROM schema_migrations");
        Assert.Equal(["001_demo.sql"], ids);
    }

    [Fact]
    public async Task ConcurrentInitializersSerializeOnTheAdvisoryLock()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        // Separate provider instances mirror separate replicas starting at the same time.
        static StubSqlFileProvider NewScripts() => new StubSqlFileProvider()
            .AddSchemaScript("001_demo.sql", CreateDemoTable)
            .AddSchemaScript("002_other.sql", CreateOtherTable);

        var runs = Enumerable.Range(0, 4)
            .Select(_ => new DatabaseInitializer().InitializeAsync(BuildServices(connectionString, NewScripts()), "release-1"))
            .ToArray();

        await Task.WhenAll(runs);

        var ids = await QueryAsync<string>(connectionString, "SELECT migration_id FROM schema_migrations ORDER BY migration_id");
        Assert.Equal(["001_demo.sql", "002_other.sql"], ids);
    }

    [Fact]
    public async Task LockIsReleasedAfterInitialization()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        var sql = new StubSqlFileProvider().AddSchemaScript("001_demo.sql", CreateDemoTable);

        await new DatabaseInitializer().InitializeAsync(BuildServices(connectionString, sql), "release-1");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var acquired = await connection.ExecuteScalarAsync<bool>(
            "SELECT pg_try_advisory_lock(@key)", new { key = MigrationLedger.AdvisoryLockKey });

        Assert.True(acquired);
    }

    private static async Task<IReadOnlyList<T>> QueryAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        return (await connection.QueryAsync<T>(sql)).ToArray();
    }

    private sealed record LedgerRow(string checksum, string release_id, long duration_milliseconds);
}
