using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Places;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

// User Story 3 (P3): the trip details page offers an owner-only Print action that
// links to the printable page.
public class TripDetailsPrintButtonTests : TestContext
{
    private IRenderedComponent<TripDetails> Render(bool isOwner, Guid tripId)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new DetailsStubTripApiClient(isOwner, tripId));
        return RenderComponent<TripDetails>(p => p.Add(x => x.TripId, tripId));
    }

    [Fact]
    public void Owner_SeesPrintLinkToPrintablePage()
    {
        var tripId = Guid.NewGuid();
        var cut = Render(isOwner: true, tripId);

        cut.WaitForAssertion(() =>
        {
            var printLink = cut.FindAll("a").First(a => a.TextContent.Trim() == "Print");
            Assert.Equal($"/trips/{tripId}/print", printLink.GetAttribute("href"));
        });
    }

    [Fact]
    public void NonOwner_DoesNotSeePrintLink()
    {
        var tripId = Guid.NewGuid();
        var cut = Render(isOwner: false, tripId);

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(cut.FindAll("a"), a => a.TextContent.Trim() == "Print");
        });
    }

    private sealed class DetailsStubTripApiClient : ITripApiClient
    {
        private readonly bool _isOwner;
        private readonly Guid _tripId;

        public DetailsStubTripApiClient(bool isOwner, Guid tripId)
        {
            _isOwner = isOwner;
            _tripId = tripId;
        }

        public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripDetail?>(
            new TripDetail(_tripId, "Sample", null, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 20),
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>(),
                _isOwner ? TripAccessLevel.Owner : TripAccessLevel.Viewer, _isOwner));

        public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripTimelineResponse?>(
            new TripTimelineResponse(_tripId, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 20), 60,
                Array.Empty<TimelineLeg>(), Array.Empty<TimelineItem>()));

        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => throw new NotSupportedException();
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
        public Task<TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(new TripMapResponse(Array.Empty<TripMapLocation>()));
        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripShareMember>>(Array.Empty<TripShareMember>());
        public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
