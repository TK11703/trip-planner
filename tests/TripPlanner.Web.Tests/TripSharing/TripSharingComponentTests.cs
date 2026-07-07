using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Auth;
using Xunit;

namespace TripPlanner.Web.Tests.TripSharing;

public class TripSharingComponentTests : TestContext
{
    [Fact]
    public void TripsIndex_ShowsOwnedAndSharedBadges()
    {
        var owned = new TripSummary(Guid.NewGuid(), "My trip", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), DateTimeOffset.UtcNow, 0, TripAccessLevel.Owner, true);
        var shared = new TripSummary(Guid.NewGuid(), "Partner trip", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), DateTimeOffset.UtcNow, 0, TripAccessLevel.Collaborator, false);
        var client = new StubShareApiClient
        {
            Trips = new TripListResponse(new[] { owned, shared }, 1, 12, 2)
        };
        Services.AddSingleton<ITripApiClient>(client);

        var cut = RenderComponent<TripsIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Owned", cut.Markup);
            Assert.Contains("Shared", cut.Markup);
            Assert.Contains("Collaborator", cut.Markup);
        });
    }

    [Fact]
    public void ShareModal_ListsCurrentMembers()
    {
        var member = new TripShareMember("user-x", "Alex Doe", "alex@example.com", TripAccessLevel.Viewer, DateTimeOffset.UtcNow);
        var client = new StubShareApiClient { Members = new[] { member } };
        Services.AddSingleton<ITripApiClient>(client);

        var cut = RenderComponent<ShareTripModal>(parameters => parameters
            .Add(p => p.TripId, Guid.NewGuid()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Currently assigned shared people", cut.Markup);
            Assert.Contains("Alex Doe", cut.Markup);
        });
    }

    private sealed class StubShareApiClient : ITripApiClient
    {
        public TripListResponse Trips { get; set; } = new(Array.Empty<TripSummary>(), 1, 12, 0);
        public IReadOnlyList<TripShareMember> Members { get; set; } = Array.Empty<TripShareMember>();

        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => Task.FromResult(Trips);
        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripSummary>>(Array.Empty<TripSummary>());
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
        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(Members);
        public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DirectoryUserResult>>(Array.Empty<DirectoryUserResult>());
        public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => Task.FromResult(new TripShareMember(request.UserId, request.DisplayName, request.Email, request.AccessLevel, DateTimeOffset.UtcNow));
        public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => Task.FromResult(new TripShareMember(userId, null, null, request.AccessLevel, DateTimeOffset.UtcNow));
        public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
