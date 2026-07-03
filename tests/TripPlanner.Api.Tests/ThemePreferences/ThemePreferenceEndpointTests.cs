using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Theme;
using TripPlanner.Database.ThemePreferences;
using Xunit;

namespace TripPlanner.Api.Tests.ThemePreferences;

public class ThemePreferenceEndpointTests
{
    [Fact]
    public async Task Get_ReturnsNoContent_WhenNoPreferenceExists()
    {
        await using var factory = new ThemePreferenceApiFactory();
        var client = factory.CreateClient();
        client.AddTestUserHeaders(TestUsers.UserA);

        var response = await client.GetAsync("/api/theme-preference");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Put_ThenGet_ReturnsCurrentUsersPreferenceOnly()
    {
        await using var factory = new ThemePreferenceApiFactory();
        var client = factory.CreateClient();
        client.AddTestUserHeaders(TestUsers.UserA);

        var put = await client.PutAsJsonAsync("/api/theme-preference", new UpdateThemePreferenceRequest(ThemeMode.Dark));
        put.EnsureSuccessStatusCode();
        var saved = await put.Content.ReadFromJsonAsync<ThemePreferenceResponse>();

        Assert.Equal(ThemeMode.Dark, saved!.ThemeMode);
        Assert.Equal(ThemePreferenceSource.AccountPreference, saved.Source);

        client.AddTestUserHeaders(TestUsers.UserB);
        var other = await client.GetAsync("/api/theme-preference");
        Assert.Equal(HttpStatusCode.NoContent, other.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequests_AreUnauthorized()
    {
        await using var factory = new ThemePreferenceApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/theme-preference");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class ThemePreferenceApiFactory : TestApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.RemoveAll<IThemePreferenceRepository>(services);
                services.AddSingleton<IThemePreferenceRepository, InMemoryThemePreferenceRepository>();
            });
        }
    }

    private sealed class InMemoryThemePreferenceRepository : IThemePreferenceRepository
    {
        private readonly Dictionary<string, ThemePreferenceRecord> _records = new(StringComparer.Ordinal);

        public Task<ThemePreferenceRecord?> GetAsync(string travelerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_records.TryGetValue(travelerId, out var record) ? record : null);

        public Task<ThemePreferenceRecord> UpsertAsync(string travelerId, ThemeMode themeMode, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            var created = _records.TryGetValue(travelerId, out var existing) ? existing.CreatedAtUtc : nowUtc;
            var record = new ThemePreferenceRecord(travelerId, themeMode, created, nowUtc);
            _records[travelerId] = record;
            return Task.FromResult(record);
        }
    }
}
