using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Auth;
using Xunit;

namespace TripPlanner.Web.Tests.Home;

public class RecentTripsComponentTests : TestContext
{
    [Fact]
    public void AnonymousUser_DoesNotCallProtectedTripApi()
    {
        var client = new RecordingTripApiClient(Array.Empty<TripSummary>());
        Services.AddSingleton<ITripApiClient>(client);
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: false));

        var cut = RenderComponent<RecentTripsList>();

        Assert.Equal(0, client.RecentCallCount);
        Assert.Contains("Sign in is required", cut.Markup);
    }

    [Fact]
    public void SignedInUser_WithNoTrips_RendersEmptyState()
    {
        var client = new RecordingTripApiClient(Array.Empty<TripSummary>());
        Services.AddSingleton<ITripApiClient>(client);
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = RenderComponent<RecentTripsList>();

        cut.WaitForAssertion(() => Assert.Contains("No trips yet", cut.Markup));
        Assert.Equal(1, client.RecentCallCount);
    }

    [Fact]
    public void SignedInUser_WithTrips_RendersCards()
    {
        var trip = new TripSummary(Guid.NewGuid(), "Paris planning", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), DateTimeOffset.UtcNow, 0);
        var client = new RecordingTripApiClient(new[] { trip });
        Services.AddSingleton<ITripApiClient>(client);
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = RenderComponent<RecentTripsList>();

        cut.WaitForAssertion(() => Assert.Contains("Paris planning", cut.Markup));
        Assert.Equal(1, client.RecentCallCount);
    }

    [Fact]
    public async Task TripApiClient_UsesSuppliedHttpClientForDetailAndMutations()
    {
        var tripId = Guid.NewGuid();
        var handler = new RecordingHttpHandler();
        var api = new TripApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test") });

        await api.GetTripsAsync(2, 5);
        await api.GetDetailAsync(tripId);
        await api.CreateAsync(new CreateTripRequest("Owner trip", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6)));
        await api.UpdateAsync(tripId, new UpdateTripRequest("Owner trip", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6)));

        Assert.Collection(handler.Requests,
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("/api/trips", request.RequestUri!.AbsolutePath);
                Assert.Equal("?page=2&pageSize=5", request.RequestUri.Query);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal($"/api/trips/{tripId}", request.RequestUri!.AbsolutePath);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/trips", request.RequestUri!.AbsolutePath);
            },
            request =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal($"/api/trips/{tripId}", request.RequestUri!.AbsolutePath);
            });
    }

    private sealed class RecordingTripApiClient : ITripApiClient
    {
        private readonly IReadOnlyList<TripSummary> _trips;
        public int RecentCallCount { get; private set; }

        public RecordingTripApiClient(IReadOnlyList<TripSummary> trips) => _trips = trips;

        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default)
            => Task.FromResult(new TripListResponse(_trips, page, pageSize, _trips.Count));

        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default)
        {
            RecentCallCount++;
            return Task.FromResult(_trips);
        }

        public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripDetail?>(null);
        public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripLegDefaultsResponse?>(null);
        public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripTimelineResponse?>(null);
        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripShareMember>>(Array.Empty<TripShareMember>());
        public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DirectoryUserResult>>(Array.Empty<DirectoryUserResult>());
        public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => Task.FromResult(new TripShareMember(request.UserId, request.DisplayName, request.Email, request.AccessLevel, DateTimeOffset.UtcNow));
        public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => Task.FromResult(new TripShareMember(userId, null, null, request.AccessLevel, DateTimeOffset.UtcNow));
        public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>>(Array.Empty<TripPlanner.Contracts.Places.PlaceSuggestion>());
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var responseJson = request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/trips"
                ? """{"trips":[],"page":2,"pageSize":5,"totalCount":0}"""
                : request.Method == HttpMethod.Get
                ? """{"tripId":"00000000-0000-0000-0000-000000000001","name":"Owner trip","description":null,"startDate":"2026-09-01","endDate":"2026-09-06","createdAtUtc":"2026-01-01T00:00:00+00:00","updatedAtUtc":"2026-01-01T00:00:00+00:00","legs":[],"trackedItems":[]}"""
                : """{"tripId":"00000000-0000-0000-0000-000000000001","name":"Owner trip","description":null,"startDate":"2026-09-01","endDate":"2026-09-06"}""";

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
