using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// User Story 1 (P1): The end reacts to one hour after the start on create and edit forms.
public class TrackedItemFormReactiveEndTests : BunitContext
{
    public TrackedItemFormReactiveEndTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    private IRenderedComponent<TrackedItemForm> RenderCreate(TripLegDto leg, DateTime start)
        => Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, start));

    [Fact]
    public void ChangingStart_SetsEndToOneHourLater()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var cut = RenderCreate(leg, new DateTime(2026, 9, 5, 9, 0, 0));

        var start = cut.FindAll("input[type=datetime-local]")[0];
        start.Change("2026-09-06T13:00");

        var end = cut.FindAll("input[type=datetime-local]")[1];
        Assert.Equal("2026-09-06T14:00:00", end.GetAttribute("value"));
    }

    [Fact]
    public void AfterManualEndEdit_StartChange_ResetsEndToOneHourLater()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var cut = RenderCreate(leg, new DateTime(2026, 9, 5, 9, 0, 0));

        // Manually set the end, then change the start again.
        cut.FindAll("input[type=datetime-local]")[1].Change("2026-09-06T20:00");
        cut.FindAll("input[type=datetime-local]")[0].Change("2026-09-07T08:00");

        var end = cut.FindAll("input[type=datetime-local]")[1];
        Assert.Equal("2026-09-07T09:00:00", end.GetAttribute("value"));
    }

    [Fact]
    public void EditingExistingItem_StartChange_SetsEndToOneHourLater()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var item = TrackedItemFormTestData.Item(leg.TripLegId, new DateTime(2026, 9, 5, 9, 0, 0), new DateTime(2026, 9, 5, 12, 0, 0));

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.Item, item));

        cut.FindAll("input[type=datetime-local]")[0].Change("2026-09-05T15:00");

        var end = cut.FindAll("input[type=datetime-local]")[1];
        Assert.Equal("2026-09-05T16:00:00", end.GetAttribute("value"));
    }
}
