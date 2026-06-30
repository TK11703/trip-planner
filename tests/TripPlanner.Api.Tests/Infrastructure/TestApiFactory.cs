using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Database.Connections;

namespace TripPlanner.Api.Tests.Infrastructure;

/// <summary>
/// WebApplicationFactory configured with a test authentication handler so endpoint
/// tests can authenticate by setting <c>X-Test-User</c> headers. Database access is
/// stubbed out via an in-process fake unless a derived fixture wires Testcontainers.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:tripplanner"] = "Host=localhost;Database=tripplanner;Username=test;Password=test"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Replace the real connection factory with a no-op for cases where DB access is not exercised.
            services.RemoveAll<IPostgresConnectionFactory>();
            services.AddSingleton<IPostgresConnectionFactory, NullPostgresConnectionFactory>();
        });
    }
}

internal static class ServiceCollectionRemoveExtensions
{
    public static IServiceCollection RemoveAll<T>(this IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(T)) services.RemoveAt(i);
        }
        return services;
    }
}

internal sealed class NullPostgresConnectionFactory : IPostgresConnectionFactory
{
    public Task<System.Data.Common.DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Database access is not configured in this test context.");
}
