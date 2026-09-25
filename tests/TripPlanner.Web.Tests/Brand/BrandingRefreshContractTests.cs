using Bunit;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Components.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Brand;

public class BrandingRefreshContractTests : BunitContext
{
    [Fact]
    public void BrandMark_RendersWireFrameGlobe_NotLegacyStar()
    {
        var cut = Render<BrandMark>();

        Assert.Contains("brand-globe", cut.Markup);
        Assert.Contains("Trip Planner", cut.Markup);
        Assert.DoesNotContain("✦", cut.Markup);
    }

    [Fact]
    public void EmptyState_UsesGlobeAndRefreshedCopy()
    {
        var cut = Render<NoTripsEmptyState>();

        Assert.Contains("empty-trip", cut.Markup);
        Assert.Contains("empty-trip-globe", cut.Markup);
        Assert.Contains("Plan your first trip", cut.Markup);
        Assert.DoesNotContain("empty-explorer", cut.Markup);
        Assert.DoesNotContain("adventure", cut.Markup);
    }
}
