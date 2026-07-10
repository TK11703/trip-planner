using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// User Story 3 (P3): Location is optional but, when entered, must be map-capable.
public class TrackedItemFormLocationValidationTests : TestContext
{
    private const string FormatMessage = "Enter a place or address that can be shown on a map.";
    private const string LengthMessage = "Location must be 200 characters or fewer.";

    public TrackedItemFormLocationValidationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    private IRenderedComponent<TrackedItemForm> RenderCreate(TripLegDto leg)
        => RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 9, 5, 9, 0, 0)));

    private static TripLegDto Leg() =>
        TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));

    [Fact]
    public void SymbolsOnlyLocation_IsRejected()
    {
        var cut = RenderCreate(Leg());
        cut.Find(".col-7 input").Change("Museum visit");
        cut.Find("#item-location").Change("!!!");

        cut.Find("form").Submit();

        Assert.Contains(FormatMessage, cut.Markup);
    }

    [Fact]
    public void OverlongLocation_IsRejected()
    {
        var cut = RenderCreate(Leg());
        cut.Find(".col-7 input").Change("Museum visit");
        cut.Find("#item-location").Change(new string('a', 201));

        cut.Find("form").Submit();

        Assert.Contains(LengthMessage, cut.Markup);
    }

    [Fact]
    public void ValidAddress_IsAccepted()
    {
        var cut = RenderCreate(Leg());
        cut.Find(".col-7 input").Change("Museum visit");
        cut.Find("#item-location").Change("Louvre Museum, Paris");

        cut.Find("form").Submit();

        Assert.DoesNotContain(FormatMessage, cut.Markup);
        Assert.DoesNotContain(LengthMessage, cut.Markup);
    }

    [Fact]
    public void EmptyLocation_IsAllowed()
    {
        var cut = RenderCreate(Leg());
        cut.Find(".col-7 input").Change("Museum visit");
        // Leave location empty.

        cut.Find("form").Submit();

        Assert.DoesNotContain(FormatMessage, cut.Markup);
        Assert.DoesNotContain(LengthMessage, cut.Markup);
    }
}
