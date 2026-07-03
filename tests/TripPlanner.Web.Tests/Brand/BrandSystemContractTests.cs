using Bunit;
using TripPlanner.Web.Components.Shared;
using Xunit;

namespace TripPlanner.Web.Tests.Brand;

public class BrandSystemContractTests : TestContext
{
    [Fact]
    public void BrandMark_RendersCompactExplorerIdentity()
    {
        var cut = RenderComponent<BrandMark>();
        Assert.Contains("Trip Planner", cut.Markup);
        Assert.Contains("brand-mark", cut.Markup);
    }

    [Fact]
    public void StateMessage_ProvidesNonColorCueAndStatusRole()
    {
        var cut = RenderComponent<StateMessage>(p => p
            .Add(x => x.Title, "Unavailable trail")
            .Add(x => x.Message, "Try again later.")
            .Add(x => x.Icon, "!")
            .Add(x => x.Tone, "warning"));
        Assert.Contains("role=\"status\"", cut.Markup);
        Assert.Contains("!", cut.Markup);
    }
}
