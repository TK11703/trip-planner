using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.Trips;

public class AuthenticatedApiContractTests : IClassFixture<TestApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticatedApiContractTests(TestApiFactory factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITripReadRepository>();
            services.RemoveAll<ITripCommandRepository>();
            services.RemoveAll<ITripItemRepository>();
            services.RemoveAll<IAuditRepository>();
            services.AddSingleton<FakeTripStore>();
            services.AddSingleton<ITripReadRepository>(sp => sp.GetRequiredService<FakeTripStore>());
            services.AddSingleton<ITripCommandRepository>(sp => sp.GetRequiredService<FakeTripStore>());
            services.AddSingleton<ITripItemRepository, EmptyTripItemRepository>();
            services.AddSingleton<IAuditRepository, InMemoryAuditRepository>();
        }));
    }

    [Fact]
    public async Task ValidScopedToken_CanGetRecentTrips()
    {
        var client = CreateAuthenticatedClient();

        var trips = await client.GetFromJsonAsync<TripSummary[]>("/api/trips/recent");

        Assert.NotNull(trips);
        Assert.Single(trips!);
        Assert.Equal(TestUsers.UserA, _factory.Services.GetRequiredService<FakeTripStore>().LastReadOwnerUserId);
    }

    [Fact]
    public async Task ValidScopedToken_CanGetPaginatedTripsForCurrentUser()
    {
        var client = CreateAuthenticatedClient();

        var trips = await client.GetFromJsonAsync<TripListResponse>("/api/trips?page=2&pageSize=5");

        Assert.NotNull(trips);
        Assert.Equal(2, trips!.Page);
        Assert.Equal(5, trips.PageSize);
        Assert.Single(trips.Trips);
        Assert.Equal(TestUsers.UserA, _factory.Services.GetRequiredService<FakeTripStore>().LastReadOwnerUserId);
    }

    [Fact]
    public async Task ValidScopedToken_CanCreateTripForCurrentUser()
    {
        var client = CreateAuthenticatedClient();
        var request = new CreateTripRequest("Owner trip", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6));

        var response = await client.PostAsJsonAsync("/api/trips", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(TestUsers.UserA, _factory.Services.GetRequiredService<FakeTripStore>().LastWriteOwnerUserId);
    }

    [Fact]
    public async Task ValidScopedToken_CanUpdateTripForCurrentUser()
    {
        var client = CreateAuthenticatedClient();
        var tripId = _factory.Services.GetRequiredService<FakeTripStore>().SeedTripId;
        var request = new UpdateTripRequest("Updated owner trip", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6));

        var response = await client.PutAsJsonAsync($"/api/trips/{tripId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TestUsers.UserA, _factory.Services.GetRequiredService<FakeTripStore>().LastWriteOwnerUserId);
    }

    [Fact]
    public async Task MissingScope_IsDenied()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, TestUsers.UserA);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestScopeHeader, "user.read");

        var response = await client.GetAsync("/api/trips/recent");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, TestUsers.UserA);
        return client;
    }

    private sealed class FakeTripStore : ITripReadRepository, ITripCommandRepository
    {
        public Guid SeedTripId { get; } = Guid.NewGuid();
        public string? LastReadOwnerUserId { get; private set; }
        public string? LastWriteOwnerUserId { get; private set; }

        public Task<TripListResponse> GetPageAsync(string ownerUserId, int page, int pageSize, CancellationToken cancellationToken)
        {
            LastReadOwnerUserId = ownerUserId;
            IReadOnlyList<TripSummary> result = new[]
            {
                new TripSummary(SeedTripId, "Owner trip", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), DateTimeOffset.UtcNow, 0)
            };
            return Task.FromResult(new TripListResponse(result, page, pageSize, result.Count));
        }

        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(string ownerUserId, int limit, CancellationToken cancellationToken)
        {
            LastReadOwnerUserId = ownerUserId;
            IReadOnlyList<TripSummary> result = new[]
            {
                new TripSummary(SeedTripId, "Owner trip", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), DateTimeOffset.UtcNow, 0)
            };
            return Task.FromResult(result);
        }

        public Task<TripDetail?> GetDetailAsync(string ownerUserId, Guid tripId, CancellationToken cancellationToken)
        {
            LastReadOwnerUserId = ownerUserId;
            TripDetail? result = tripId == SeedTripId
                ? new TripDetail(SeedTripId, "Owner trip", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>())
                : null;
            return Task.FromResult(result);
        }

        public Task<Guid> InsertAsync(string ownerUserId, CreateTripRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            LastWriteOwnerUserId = ownerUserId;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<int> UpdateAsync(string ownerUserId, Guid tripId, UpdateTripRequest request, CancellationToken cancellationToken)
        {
            LastWriteOwnerUserId = ownerUserId;
            return Task.FromResult(tripId == SeedTripId ? 1 : 0);
        }
    }

    private sealed class EmptyTripItemRepository : ITripItemRepository
    {
        public Task<IReadOnlyList<TripLegDto>> GetLegsAsync(string ownerUserId, Guid tripId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TripLegDto>>(Array.Empty<TripLegDto>());
        public Task<IReadOnlyList<TrackedItemDto>> GetTrackedItemsAsync(string ownerUserId, Guid tripId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TrackedItemDto>>(Array.Empty<TrackedItemDto>());
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(string ownerUserId, Guid tripId, CancellationToken ct) => Task.FromResult<TripLegDefaultsResponse?>(new TripLegDefaultsResponse("UTC", "UTC", "profile"));
        public Task<Guid?> CreateLegAsync(string ownerUserId, Guid tripId, CreateTripLegRequest request, DateTimeOffset nowUtc, CancellationToken ct) => Task.FromResult<Guid?>(Guid.NewGuid());
        public Task<int> UpdateLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct) => Task.FromResult(1);
        public Task<int> DeleteLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct) => Task.FromResult(1);
        public Task<Guid?> CreateTrackedItemAsync(string ownerUserId, Guid tripId, CreateTrackedItemRequest request, DateTimeOffset nowUtc, CancellationToken ct) => Task.FromResult<Guid?>(Guid.NewGuid());
        public Task<int> UpdateTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct) => Task.FromResult(1);
        public Task<int> DeleteTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, CancellationToken ct) => Task.FromResult(1);
        public Task<int> CountItemsForLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class InMemoryAuditRepository : IAuditRepository
    {
        public Task RecordAsync(string? userId, string operation, string resourceType, string? resourceId, string result, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
