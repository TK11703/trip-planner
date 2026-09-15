using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TripPlanner.Web.Health;

/// <summary>
/// Readiness check: the API is reachable from the Web replica.
/// </summary>
/// <remarks>
/// Probes the API's own liveness endpoint, which does not touch remote dependencies, so a
/// slow database does not cascade into Web replicas being pulled from traffic.
/// </remarks>
public sealed class ApiReachabilityHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public const string Name = "api-reachability";
    public const string HttpClientName = "api-health";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync("/alive", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The API liveness endpoint returned a failure status.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("The API is not reachable.", ex);
        }
    }
}

/// <summary>
/// Readiness check: every Entra setting the sign-in flow needs is present.
/// </summary>
public sealed class AuthenticationConfigurationHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public const string Name = "authentication-configuration";

    private static readonly string[] RequiredKeys =
    [
        "AzureEntra:Instance",
        "AzureEntra:TenantId",
        "AzureEntra:ClientId",
        "AzureEntra:ApiScopes"
    ];

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var missing = RequiredKeys.Where(key => !IsConfigured(configuration, key)).ToArray();

        return Task.FromResult(missing.Length == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Missing authentication configuration: {string.Join(", ", missing)}."));
    }

    /// <summary>
    /// True when the key has a value or, for array-valued settings such as
    /// <c>AzureEntra:ApiScopes</c>, at least one child entry.
    /// </summary>
    private static bool IsConfigured(IConfiguration configuration, string key)
    {
        var section = configuration.GetSection(key);
        return !string.IsNullOrWhiteSpace(section.Value) || section.GetChildren().Any();
    }
}

/// <summary>
/// Readiness check: the data-protection key ring is usable.
/// </summary>
/// <remarks>
/// Protect/unprotect round-trips a token. That forces the key ring to be resolved from its
/// persistence store, so a replica whose blob container or Key Vault key is unreachable
/// fails readiness instead of silently issuing cookies that other replicas cannot read.
/// </remarks>
public sealed class DataProtectionHealthCheck(IDataProtectionProvider provider) : IHealthCheck
{
    public const string Name = "data-protection";
    private const string Purpose = "TripPlanner.Web.HealthCheck";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var protector = provider.CreateProtector(Purpose);
            var roundTripped = protector.Unprotect(protector.Protect(Purpose));

            return Task.FromResult(string.Equals(roundTripped, Purpose, StringComparison.Ordinal)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The data-protection key ring did not round-trip a payload."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The data-protection key ring is unavailable.", ex));
        }
    }
}
