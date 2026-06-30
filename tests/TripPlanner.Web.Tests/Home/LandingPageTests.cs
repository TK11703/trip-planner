using Bunit;
using TripPlanner.Web.Components.Pages;
using Xunit;

namespace TripPlanner.Web.Tests.Home;

public class LandingPageTests : TestContext
{
    [Fact(Skip = "Requires AuthorizationTestExtensions setup to render Home with anonymous state.")]
    public void AnonymousHome_RendersHero_WithoutPersonalData() { }
}
