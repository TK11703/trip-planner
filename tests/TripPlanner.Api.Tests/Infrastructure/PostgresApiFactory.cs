using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Api.Tests.Infrastructure;

/// <summary>
/// The API wired to a real PostgreSQL instance rather than in-memory fakes.
///
/// <see cref="TestApiFactory"/> replaces the connection factory with one that throws, which is
/// right for endpoints whose behaviour has nothing to do with storage. It is exactly wrong for
/// the handful of guarantees that <em>are</em> storage: that one traveler cannot read another's
/// trip, that a denied attempt leaves an audit row, and that the row carries no secret. Those
/// live in SQL predicates and table constraints, so a fake repository asked the same questions
/// would only ever confirm the test author's assumptions.
///
/// One container is shared by every class in the <see cref="ApiPostgresCollectionName"/>
/// collection; tests seed their own user ids so they neither collide nor depend on ordering.
/// </summary>
public class PostgresApiFactory : TestApiFactory, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("tripplanner_api_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        foreach (var (_, body) in new SqlFileProvider().GetAllInDirectory("Schema"))
        {
            await conn.ExecuteAsync(body);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            // Undo the base factory's throwing connection factory: these tests want the real
            // repositories talking to a real database.
            services.RemoveAll<IPostgresConnectionFactory>();
            services.AddSingleton<IPostgresConnectionFactory>(new ContainerConnectionFactory(ConnectionString));
        });
    }

    /// <summary>A connection to the same database the API under test is using.</summary>
    public async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        Dispose();
    }

    public const string ApiPostgresCollectionName = "ApiPostgres";

    private sealed class ContainerConnectionFactory(string connectionString) : IPostgresConnectionFactory
    {
        public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}

/// <summary>
/// Shares one container-backed API host across the storage-dependent test classes. Named without
/// a <c>Collection</c> suffix so it does not trip CA1711.
/// </summary>
[CollectionDefinition(PostgresApiFactory.ApiPostgresCollectionName)]
public sealed class SharedApiPostgres : ICollectionFixture<PostgresApiFactory>;
