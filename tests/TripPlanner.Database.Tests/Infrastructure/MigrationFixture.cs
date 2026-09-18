using System.Data.Common;
using Npgsql;
using Testcontainers.PostgreSql;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Tests.Infrastructure;

/// <summary>
/// A PostgreSQL container used by migration tests. Each test gets its own freshly created
/// database so migration state from one test cannot leak into another.
/// </summary>
public sealed class MigrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("migrations_root")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Creates an empty database and returns its connection string.
    /// </summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"mig_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name };
        return builder.ConnectionString;
    }
}

/// <summary>
/// Minimal <see cref="IServiceProvider"/> so migration tests do not need a DI container.
/// </summary>
public sealed class StubServiceProvider(params (Type Type, object Instance)[] services) : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = services.ToDictionary(s => s.Type, s => s.Instance);

    public object? GetService(Type serviceType) =>
        _services.TryGetValue(serviceType, out var instance) ? instance : null;
}

public sealed class StubConnectionFactory(string connectionString) : IPostgresConnectionFactory
{
    public async Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

/// <summary>
/// Serves migration scripts from an in-memory map so tests can control script content,
/// ordering, and edits. The real ledger bootstrap script is reused verbatim.
/// </summary>
public sealed class StubSqlFileProvider : ISqlFileProvider
{
    private static readonly string LedgerBootstrap =
        new SqlFileProvider().Get("Initialization/migration_ledger.sql");

    private readonly List<(string Name, string Sql)> _schema = [];

    public StubSqlFileProvider AddSchemaScript(string name, string sql)
    {
        _schema.Add((name, sql));
        return this;
    }

    public void ReplaceSchemaScript(string name, string sql)
    {
        var index = _schema.FindIndex(s => s.Name == name);
        _schema[index] = (name, sql);
    }

    public string Get(string relativePath) => relativePath switch
    {
        "Initialization/migration_ledger.sql" => LedgerBootstrap,
        _ => throw new FileNotFoundException(relativePath)
    };

    public IReadOnlyList<(string Name, string Sql)> GetAllInDirectory(string relativeDirectory) =>
        relativeDirectory == "Schema" ? _schema.ToArray() : [];
}
