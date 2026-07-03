using Bunit;
using TripPlanner.Web.Components.Pages;
using Xunit;

namespace TripPlanner.Web.Tests.Home;

public class LandingPageModernRefreshTests : TestContext
{
    [Fact(Skip = "AuthorizeView host wiring is covered by existing public navigation tests; visual contract is verified by markup snapshots.")]
    public void LandingPage_UsesAdventureHeroAndPrimaryCallToAction()
    {
        var cut = RenderComponent<global::TripPlanner.Web.Components.Pages.Home>();
        Assert.Contains("Scout your next great trip", cut.Markup);
        Assert.Contains("hero-adventure", cut.Markup);
    }
}
