using Bunit;
using TripPlanner.Web.Components.Pages;
using Xunit;

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
}
