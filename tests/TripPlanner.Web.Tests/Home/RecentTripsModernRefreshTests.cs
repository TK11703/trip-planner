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

public class RecentTripsModernRefreshTests : TestContext
{
    [Fact]
    public void EmptyRecentTrips_UsesBrandedRecoveryState()
    {
        Services.AddSingleton<ITripApiClient>(new EmptyTripApiClient());
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = RenderComponent<RecentTripsList>();

        cut.WaitForAssertion(() => Assert.Contains("empty-trip", cut.Markup));
        cut.WaitForAssertion(() => Assert.Contains("Plan your first trip", cut.Markup));
    }

    private sealed class EmptyTripApiClient : ITripApiClient
    {
        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => Task.FromResult(new TripListResponse([], page, pageSize, 0));
        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripSummary>>([]);
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
        public Task<TripPlanner.Contracts.Trips.TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(new TripPlanner.Contracts.Trips.TripMapResponse(Array.Empty<TripPlanner.Contracts.Trips.TripMapLocation>()));
    }
}
