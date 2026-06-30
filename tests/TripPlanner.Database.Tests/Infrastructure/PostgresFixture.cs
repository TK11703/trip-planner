using Dapper;
using DotNet.Testcontainers.Builders;
using Testcontainers.PostgreSql;
using Xunit;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
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
