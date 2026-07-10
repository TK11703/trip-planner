using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Api.Features.Places;
using TripPlanner.Api.Security;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.TripSharing;
using Xunit;

namespace TripPlanner.Api.Tests.TripMaps;

public class GetTripMapEndpointTests
{
    private static readonly Guid SeedTripId = Guid.NewGuid();

    private static TrackedItemDto Item(string title, string? location)
        => new(Guid.NewGuid(), SeedTripId, null, "activity", title, location,
            DateTime.Now, "UTC", DateTimeOffset.UtcNow, null, null, null, "blue", null, null, 0);

    private static WebApplicationFactory<Program> Factory(
        IReadOnlyList<TrackedItemDto> items,
        IReadOnlyDictionary<string, GeoPoint> resolved,
        Guid? ownedTripId = null)
    {
        var owned = ownedTripId ?? SeedTripId;
        return new TestApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITripItemRepository>();
            services.RemoveAll<IAuditRepository>();
            services.RemoveAll<ITripAccessResolver>();
            services.RemoveAll<IPlaceGeocoder>();
            services.AddSingleton<ITripItemRepository>(new FakeItemRepository(items));
            services.AddSingleton<IAuditRepository, NoopAuditRepository>();
            services.AddSingleton<ITripAccessResolver>(new FakeAccessResolver(owned));
            services.AddSingleton<IPlaceGeocoder>(new FakeGeocoder(resolved));
        }));
    }

    private static HttpClient Authenticated(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, TestUsers.UserA);
        return client;
    }

    [Fact]
    public async Task Owner_ReturnsOnlyResolvedLocations_AndFansSharedText()
    {
        var items = new[]
        {
            Item("Louvre visit", "Paris"),
            Item("Coffee", "Paris"),           // same text -> shares coordinates
            Item("Mystery", "zzz nowhere zzz"),// unresolved -> omitted
            Item("No place", null)              // no location -> omitted
        };
        var resolved = new Dictionary<string, GeoPoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["Paris"] = new GeoPoint(48.86, 2.34)
        };
        await using var factory = Factory(items, resolved);
        var client = Authenticated(factory);

        var response = await client.GetFromJsonAsync<TripMapResponse>($"/api/trips/{SeedTripId}/map");

        Assert.NotNull(response);
        Assert.Equal(2, response!.Locations.Count);
        Assert.All(response.Locations, l =>
        {
            Assert.Equal("Paris", l.Location);
            Assert.Equal(48.86, l.Latitude, 2);
            Assert.Equal(2.34, l.Longitude, 2);
        });
        Assert.Contains(response.Locations, l => l.Title == "Louvre visit");
        Assert.Contains(response.Locations, l => l.Title == "Coffee");
    }

    [Fact]
    public async Task NonOwnerOrUnknownTrip_ReturnsNotFound()
    {
        // The resolver owns a different trip, so the seed trip is not accessible to the caller.
        await using var factory = Factory(Array.Empty<TrackedItemDto>(), new Dictionary<string, GeoPoint>(), ownedTripId: Guid.NewGuid());
        var client = Authenticated(factory);

        var response = await client.GetAsync($"/api/trips/{SeedTripId}/map");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NothingResolves_ReturnsEmptyOk()
    {
        var items = new[] { Item("Louvre", "Paris"), Item("Tower", "London") };
        await using var factory = Factory(items, new Dictionary<string, GeoPoint>()); // geocoder resolves nothing
        var client = Authenticated(factory);

        var response = await client.GetAsync($"/api/trips/{SeedTripId}/map");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await response.Content.ReadFromJsonAsync<TripMapResponse>();
        Assert.NotNull(map);
        Assert.Empty(map!.Locations);
    }

    private sealed class FakeItemRepository : ITripItemRepository
    {
        private readonly IReadOnlyList<TrackedItemDto> _items;
        public FakeItemRepository(IReadOnlyList<TrackedItemDto> items) => _items = items;

        public Task<IReadOnlyList<TrackedItemDto>> GetTrackedItemsAsync(string ownerUserId, Guid tripId, CancellationToken ct)
            => Task.FromResult(_items);

        public Task<IReadOnlyList<TripLegDto>> GetLegsAsync(string ownerUserId, Guid tripId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripLegDto>>(Array.Empty<TripLegDto>());
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(string ownerUserId, Guid tripId, CancellationToken ct) => Task.FromResult<TripLegDefaultsResponse?>(null);
        public Task<Guid?> CreateLegAsync(string ownerUserId, Guid tripId, CreateTripLegRequest request, DateTimeOffset nowUtc, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<int> UpdateLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct) => Task.FromResult(0);
        public Task<int> DeleteLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct) => Task.FromResult(0);
        public Task<Guid?> CreateTrackedItemAsync(string ownerUserId, Guid tripId, CreateTrackedItemRequest request, DateTimeOffset nowUtc, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<int> UpdateTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct) => Task.FromResult(0);
        public Task<int> DeleteTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, CancellationToken ct) => Task.FromResult(0);
        public Task<int> CountItemsForLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class FakeGeocoder : IPlaceGeocoder
    {
        private readonly IReadOnlyDictionary<string, GeoPoint> _resolved;
        public FakeGeocoder(IReadOnlyDictionary<string, GeoPoint> resolved) => _resolved = resolved;
        public bool IsConfigured => true;
        public Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct)
            => Task.FromResult(_resolved.TryGetValue(query.Trim(), out var point) ? point : (GeoPoint?)null);
    }

    private sealed class FakeAccessResolver : ITripAccessResolver
    {
        private readonly Guid _ownedTripId;
        public FakeAccessResolver(Guid ownedTripId) => _ownedTripId = ownedTripId;
        public Task<TripAccess?> ResolveAsync(string callerUserId, Guid tripId, CancellationToken ct)
            => Task.FromResult(tripId == _ownedTripId ? new TripAccess(callerUserId, TripAccessLevel.Owner) : null);
    }

    private sealed class NoopAuditRepository : IAuditRepository
    {
        public Task RecordAsync(string? userId, string operation, string resourceType, string? resourceId, string result, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
