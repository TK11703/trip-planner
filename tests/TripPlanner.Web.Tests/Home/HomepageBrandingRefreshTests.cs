using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Infrastructure;
using Xunit;
using HomePage = TripPlanner.Web.Components.Pages.Home;

namespace TripPlanner.Web.Tests.Home;

public class HomepageBrandingRefreshTests : TestContext
{
    [Fact]
    public void AnonymousHome_IsImageLed_WithTransportCuesAndPlanningTips()
    {
        this.AddTestAuthorization().SetNotAuthorized();
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient());

        var cut = RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<HomePage>());

        Assert.Contains("hero-welcome", cut.Markup);
        Assert.Contains("img/brand/home-welcome.svg", cut.Markup);
        Assert.Contains("Plan your next great trip", cut.Markup);

        // Transport references: car, train, plane, boat.
        Assert.Contains("Car", cut.Markup);
        Assert.Contains("Train", cut.Markup);
        Assert.Contains("Plane", cut.Markup);
        Assert.Contains("Boat", cut.Markup);

        // Planning tips for successful trip planning.
        Assert.Contains("Small habits that make big trips easier", cut.Markup);

        // Primary and secondary actions for a visitor.
        Assert.Contains("Sign in to begin", cut.Markup);
        Assert.Contains("See how it works", cut.Markup);
    }

    [Fact]
    public void AuthorizedHome_RetainsRecentTripsNavigation()
    {
        this.AddTestAuthorization().SetAuthorized("traveler@example.com");
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient());

        var cut = RenderComponent<CascadingAuthenticationState>(p => p.AddChildContent<HomePage>());

        cut.WaitForAssertion(() => Assert.Contains("Recent trips", cut.Markup));
        Assert.Contains("Plan a trip", cut.Markup);
    }
}
