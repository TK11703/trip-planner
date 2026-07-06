using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Auth;
using Xunit;
using HomePage = TripPlanner.Web.Components.Pages.Home;

namespace TripPlanner.Web.Tests.PublicContent;

public class PublicPageTests : TestContext
{
    [Fact]
    public void FaqPage_RendersHeader()
    {
        var cut = RenderComponent<Faq>();
        Assert.Contains("Frequently asked questions", cut.Markup);
    }

    [Fact]
    public void AboutPage_RendersPurpose()
    {
        var cut = RenderComponent<About>();
        Assert.Contains("Trip Planner", cut.Markup);
    }

    [Fact]
    public void AnonymousHome_RendersPublicContentWithoutCallingProtectedTrips()
    {
        var client = new ThrowingTripApiClient();
        this.AddTestAuthorization().SetNotAuthorized();
        Services.AddSingleton<ITripApiClient>(client);

        var cut = RenderComponent<CascadingAuthenticationState>(parameters => parameters.AddChildContent<HomePage>());

        Assert.Contains("Sign in to begin", cut.Markup);
        Assert.DoesNotContain("Recent trips", cut.Markup);
        Assert.False(client.WasCalled);
    }

    private sealed class ThrowingTripApiClient : ITripApiClient
    {
        public bool WasCalled { get; private set; }
        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) { WasCalled = true; throw new InvalidOperationException("Public pages must not load protected trip data."); }
        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) { WasCalled = true; throw new InvalidOperationException("Public pages must not load protected trip data."); }
        public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripLegDefaultsResponse?>(null);
        public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<TimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
    }
}
