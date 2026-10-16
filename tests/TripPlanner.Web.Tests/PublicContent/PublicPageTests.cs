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

public class PublicPageTests : BunitContext
{
    [Fact]
    public void FaqPage_RendersHeader()
    {
        var cut = Render<Faq>();
        Assert.Contains("Frequently asked questions", cut.Markup);
        AssertBreadcrumbs(cut, "FAQ");
    }

    [Fact]
    public void AboutPage_RendersPurpose()
    {
        var cut = Render<About>();
        Assert.Contains("Trip Planner", cut.Markup);
        AssertBreadcrumbs(cut, "About");
    }

    private static void AssertBreadcrumbs<TComponent>(IRenderedComponent<TComponent> cut, string currentPage)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        var breadcrumb = Assert.Single(cut.FindAll("nav[aria-label='Breadcrumb']"));
        Assert.Equal(["Home", currentPage],
            breadcrumb.QuerySelectorAll(".breadcrumb-item").Select(item => item.TextContent.Trim()));
        var home = Assert.Single(breadcrumb.QuerySelectorAll("a"));
        Assert.Equal("Home", home.TextContent);
        Assert.Equal("/", home.GetAttribute("href"));
        var current = Assert.Single(breadcrumb.QuerySelectorAll("[aria-current='page']"));
        Assert.Equal(currentPage, current.TextContent);
        Assert.Null(current.QuerySelector("a"));
    }

    [Fact]
    public void AnonymousHome_RendersPublicContentWithoutCallingProtectedTrips()
    {
        var client = new ThrowingTripApiClient();
        this.AddAuthorization().SetNotAuthorized();
        Services.AddSingleton<ITripApiClient>(client);

        var cut = Render<CascadingAuthenticationState>(parameters => parameters.AddChildContent<HomePage>());

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
        public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripLegDefaultsResponse?>(null);
        public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) { WasCalled = true; throw new NotSupportedException(); }
        public Task<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>>(Array.Empty<TripPlanner.Contracts.Places.PlaceSuggestion>());
        public Task<TripPlanner.Contracts.Trips.TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(new TripPlanner.Contracts.Trips.TripMapResponse(Array.Empty<TripPlanner.Contracts.Trips.TripMapLocation>()));
    }
}
