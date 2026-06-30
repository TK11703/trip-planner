using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Initialization;

public sealed class DatabaseInitializer
{
    private readonly ILogger<DatabaseInitializer>? _logger;

    public DatabaseInitializer(ILogger<DatabaseInitializer>? logger = null) => _logger = logger;

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var factory = services.GetRequiredService<IPostgresConnectionFactory>();
        var sql = services.GetRequiredService<ISqlFileProvider>();

        var scripts = sql.GetAllInDirectory("Schema");
        if (scripts.Count == 0)
        {
            _logger?.LogInformation("No schema scripts located; skipping database initialization.");
            return;
        }

        await using var conn = await factory.CreateOpenConnectionAsync(cancellationToken);
        foreach (var (name, body) in scripts)
        {
            _logger?.LogInformation("Applying schema script {Script}", name);
            await conn.ExecuteAsync(new CommandDefinition(body, cancellationToken: cancellationToken));
        }
    }
}
