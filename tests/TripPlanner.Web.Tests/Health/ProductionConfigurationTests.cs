using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TripPlanner.Web.Extensions;
using TripPlanner.Web.Health;

namespace TripPlanner.Web.Tests.Health;

public class ProductionConfigurationTests
{
    private static readonly HealthCheckContext Context = new()
    {
        Registration = new HealthCheckRegistration("test", _ => null!, HealthStatus.Unhealthy, null)
    };

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    private static (string Key, string? Value)[] CompleteAuthConfiguration() =>
    [
        ("AzureEntra:Instance", "https://login.microsoftonline.com/"),
        ("AzureEntra:TenantId", "00000000-0000-0000-0000-000000000001"),
        ("AzureEntra:ClientId", "00000000-0000-0000-0000-000000000002"),
        ("AzureEntra:ApiScopes", "api://trip-planner/access_as_user")
    ];

    [Fact]
    public async Task Authentication_IsHealthy_WhenAllRequiredKeysArePresent()
    {
        var check = new AuthenticationConfigurationHealthCheck(Configuration(CompleteAuthConfiguration()));

        var result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Theory]
    [InlineData("AzureEntra:Instance")]
    [InlineData("AzureEntra:TenantId")]
    [InlineData("AzureEntra:ClientId")]
    [InlineData("AzureEntra:ApiScopes")]
    public async Task Authentication_IsUnhealthy_WhenARequiredKeyIsMissing(string missingKey)
    {
        var values = CompleteAuthConfiguration()
            .Select(pair => pair.Key == missingKey ? (pair.Key, (string?)null) : pair)
            .ToArray();

        var check = new AuthenticationConfigurationHealthCheck(Configuration(values));

        var result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Authentication_IsHealthy_WhenApiScopesAreSuppliedAsAnArray()
    {
        // Container Apps supplies array settings as indexed keys, not a single value.
        var values = CompleteAuthConfiguration()
            .Where(pair => pair.Key != "AzureEntra:ApiScopes")
            .Append(("AzureEntra:ApiScopes:0", (string?)"api://trip-planner-api/access_as_user"))
            .ToArray();

        var check = new AuthenticationConfigurationHealthCheck(Configuration(values));

        var result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task DataProtection_IsHealthy_WhenTheKeyRingRoundTrips()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("TripPlanner");
        using var provider = services.BuildServiceProvider();

        var check = new DataProtectionHealthCheck(provider.GetRequiredService<IDataProtectionProvider>());

        var result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task DataProtection_IsUnhealthy_WhenTheKeyRingIsUnavailable()
    {
        var check = new DataProtectionHealthCheck(new ThrowingDataProtectionProvider());

        var result = await check.CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void DataProtection_UsesInContainerKeyRing_WhenBlobUriIsNotConfigured()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddTripPlannerDataProtection();

        // No blob URI configured, so nothing is registered and the default key ring stands.
        Assert.DoesNotContain(builder.Services, d => d.ServiceType == typeof(IKeyManager));
    }

    [Fact]
    public void DataProtection_PersistsKeysExternally_WhenBlobUriIsConfigured()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataProtection:BlobUri"] = "https://example.blob.core.windows.net/dataprotection/keys.xml",
            ["DataProtection:KeyVaultKeyUri"] = "https://example.vault.azure.net/keys/dataprotection"
        });

        builder.AddTripPlannerDataProtection();

        // Registration of the key-management stack is what moves the key ring off the
        // container file system and makes it shared across replicas.
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IKeyManager));
    }

    private sealed class ThrowingDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) =>
            throw new InvalidOperationException("Key ring unavailable.");
    }
}
