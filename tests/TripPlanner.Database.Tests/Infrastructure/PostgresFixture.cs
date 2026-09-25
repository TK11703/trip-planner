using Dapper;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;
using Xunit;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("tripplanner_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => Container.GetConnectionString();
    public SqlFileProvider Sql { get; } = new SqlFileProvider();

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        await using var conn = new Npgsql.NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        foreach (var (_, body) in Sql.GetAllInDirectory("Schema"))
        {
            await conn.ExecuteAsync(body);
        }
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}

/// <summary>
/// One PostgreSQL container shared by every test that just needs the current schema.
///
/// Declared as a collection rather than a per-class fixture because xUnit creates an
/// <c>IClassFixture</c> instance per class: with a dozen repository test classes that meant a
/// dozen container starts. Sharing one costs a single start for the whole suite.
///
/// The trade is that rows are visible across classes, so every test seeds its own ids —
/// <c>user-{Guid.NewGuid():N}</c> and fresh trip ids — rather than relying on an empty table.
/// That is already the convention these tests follow, and it is what makes them safe to run in
/// any order.
///
/// Named without a <c>Collection</c> suffix so it does not trip CA1711.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedPostgres : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
