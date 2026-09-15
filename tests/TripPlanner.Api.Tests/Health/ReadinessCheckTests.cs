using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TripPlanner.Api.Health;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Database.Initialization;

namespace TripPlanner.Api.Tests.Health;

public class ReadinessCheckTests
{
    private static readonly HealthCheckContext Context = new()
    {
        Registration = new HealthCheckRegistration("test", _ => null!, HealthStatus.Unhealthy, null)
    };

    private static IConfiguration Configuration(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value))
            .Build();

    [Fact]
    public async Task Migration_IsUnhealthy_BeforeInitializationCompletes()
    {
        var result = await new MigrationHealthCheck(new DatabaseMigrationState()).CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Migration_IsUnhealthy_WhenInitializationFailed()
    {
        var state = new DatabaseMigrationState();
        state.MarkFailed();

        var result = await new MigrationHealthCheck(state).CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Migration_IsHealthy_OnceInitializationCompletes()
    {
        var state = new DatabaseMigrationState();
        state.MarkCompleted();

        var result = await new MigrationHealthCheck(state).CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Database_IsUnhealthy_WhenPostgresIsUnavailable()
    {
        var result = await new DatabaseHealthCheck(new NullPostgresConnectionFactory()).CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("http://insecure.openai.azure.com/")]
    public async Task AzureOpenAI_IsUnhealthy_WhenEndpointIsMissingOrInvalid(string? endpoint)
    {
        var configuration = Configuration(("AzureOpenAI:Endpoint", endpoint));

        var result = await new AzureOpenAIConfigurationHealthCheck(configuration).CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task AzureOpenAI_IsHealthy_WhenEndpointIsConfigured()
    {
        var configuration = Configuration(("AzureOpenAI:Endpoint", "https://example.openai.azure.com/"));

        var result = await new AzureOpenAIConfigurationHealthCheck(configuration).CheckHealthAsync(Context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}

public class HealthEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public HealthEndpointTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Alive_IsMapped_OutsideDevelopment()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Health_ReportsUnhealthy_WhenDependenciesAreUnavailable()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("{\"status\":\"Unhealthy\"}", body);
        // Aggregate only: no dependency names leak to an unauthenticated caller.
        Assert.DoesNotContain("database", body, StringComparison.OrdinalIgnoreCase);
    }
}
