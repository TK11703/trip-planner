using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Initialization;

namespace TripPlanner.Api.Health;

/// <summary>
/// Readiness check: the API can open a connection to PostgreSQL and run a trivial query.
/// </summary>
public sealed class DatabaseHealthCheck(IPostgresConnectionFactory connectionFactory) : IHealthCheck
{
    public const string Name = "database";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // The exception is attached for telemetry only; the public endpoint writes the
            // aggregate status and never this description.
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", ex);
        }
    }
}

/// <summary>
/// Readiness check: startup migrations finished successfully.
/// </summary>
public sealed class MigrationHealthCheck(DatabaseMigrationState state) : IHealthCheck
{
    public const string Name = "database-migration";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(state.Status switch
        {
            DatabaseMigrationStatus.Completed => HealthCheckResult.Healthy(),
            DatabaseMigrationStatus.Failed => HealthCheckResult.Unhealthy("Database migration failed."),
            _ => HealthCheckResult.Unhealthy("Database migration has not completed.")
        });
}

/// <summary>
/// Readiness check: the Azure OpenAI configuration the API needs for email ingestion is
/// present and well formed. Validated here rather than only at startup so a misconfigured
/// revision is held out of traffic instead of crash-looping.
/// </summary>
public sealed class AzureOpenAIConfigurationHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public const string Name = "azure-openai-configuration";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["AzureOpenAI:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("AzureOpenAI:Endpoint is not configured."));
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("AzureOpenAI:Endpoint is not an absolute HTTPS URI."));
        }

        // DeploymentName is intentionally not required: the client falls back to a default.
        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
