using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// User Story 3 (P3): A globe action opens a valid location on a map, and is absent otherwise.
public class TrackedItemFormLocationMapTests : TestContext
{
    public TrackedItemFormLocationMapTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    private IRenderedComponent<TrackedItemForm> RenderCreate()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        return RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 9, 5, 9, 0, 0)));
    }

    [Fact]
    public void ValidLocation_ShowsSafeMapLink_UsingEnteredText()
    {
        var cut = RenderCreate();
        cut.Find("#item-location").Change("Louvre Museum, Paris");

        var link = cut.Find("a.tp-location-map");
        var href = link.GetAttribute("href");
        Assert.Contains("https://www.bing.com/maps?q=", href);
        Assert.Contains("Louvre%20Museum", href);
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Contains("noopener", link.GetAttribute("rel"));
    }

    [Fact]
    public void EmptyLocation_ShowsNoMapLink()
    {
        var cut = RenderCreate();
        // Location left empty.

        Assert.Empty(cut.FindAll("a.tp-location-map"));
        var disabled = cut.Find("button.tp-location-map");
        Assert.True(disabled.HasAttribute("disabled"));
    }

    [Fact]
    public void InvalidLocation_ShowsNoMapLink()
    {
        var cut = RenderCreate();
        cut.Find("#item-location").Change("!!!");

        Assert.Empty(cut.FindAll("a.tp-location-map"));
        Assert.True(cut.Find("button.tp-location-map").HasAttribute("disabled"));
    }
}
