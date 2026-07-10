using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Places;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

// User Story 1 (P1): the built-in map modal shows an empty state when nothing resolves,
// and renders a map canvas when locations exist.
public class TripMapModalTests : TestContext
{
    private IRenderedComponent<TripMapModal> Render(TripMapResponse map)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new MapStubTripApiClient(map));
        return RenderComponent<TripMapModal>(p => p.Add(x => x.TripId, Guid.NewGuid()));
    }

    [Fact]
    public void NoLocations_ShowsEmptyState_AndNoCanvas()
    {
        var cut = Render(new TripMapResponse(Array.Empty<TripMapLocation>()));

        Assert.Empty(cut.FindAll(".tp-map-canvas"));
        Assert.Contains("couldn't place", cut.Markup);
    }

    [Fact]
    public void WithLocations_RendersMapCanvas()
    {
        var map = new TripMapResponse(new[]
        {
            new TripMapLocation(Guid.NewGuid(), "Louvre", "Paris", 48.86, 2.34)
        });

        var cut = Render(map);

        Assert.Single(cut.FindAll(".tp-map-canvas"));
    }

    [Fact]
    public async Task OnMarkerActivated_InvokesOpenEventCallback()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var eventId = Guid.NewGuid();
        var map = new TripMapResponse(new[] { new TripMapLocation(eventId, "Louvre", "Paris", 48.86, 2.34) });
        Services.AddSingleton<ITripApiClient>(new MapStubTripApiClient(map));

        Guid? opened = null;
        var cut = RenderComponent<TripMapModal>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.OnOpenEvent, EventCallback.Factory.Create<Guid>(this, id => opened = id)));

        await cut.InvokeAsync(() => cut.Instance.OnMarkerActivated(eventId));

        Assert.Equal(eventId, opened);
    }

    private sealed class MapStubTripApiClient : ITripApiClient
    {
        private readonly TripMapResponse _map;
        public MapStubTripApiClient(TripMapResponse map) => _map = map;

        public Task<TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(_map);

        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
