using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Maps;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// User Story 2 (P2): the globe opens the entered location in the user's preferred map provider.
public class TrackedItemFormMapProviderTests : TestContext
{
    private IRenderedComponent<TrackedItemForm> RenderCreate(string provider)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
        Services.AddSingleton<IMapPreferenceProvider>(new StubMapPreferenceProvider(provider));

        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        return RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 9, 5, 9, 0, 0)));
    }

    [Fact]
    public void BingPreference_OpensBingMaps()
    {
        var cut = RenderCreate("Bing");
        cut.Find("#item-location").Change("Louvre Museum, Paris");

        var href = cut.Find("a.tp-location-map").GetAttribute("href");
        Assert.StartsWith("https://www.bing.com/maps?q=", href);
        Assert.Contains("Louvre%20Museum", href);
    }

    [Fact]
    public void GooglePreference_OpensGoogleMaps()
    {
        var cut = RenderCreate("Google");
        cut.Find("#item-location").Change("Louvre Museum, Paris");

        var href = cut.Find("a.tp-location-map").GetAttribute("href");
        Assert.StartsWith("https://www.google.com/maps/search/?api=1&query=", href);
        Assert.Contains("Louvre%20Museum", href);
    }
}
