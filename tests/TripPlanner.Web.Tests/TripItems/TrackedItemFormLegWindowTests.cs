using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// An item belongs to a trip leg, so its date pickers are bounded to that leg's travel window.
public class TrackedItemFormLegWindowTests : TestContext
{
    public TrackedItemFormLegWindowTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    [Fact]
    public void DatePickers_AreBoundedToLegTravelWindow()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));

        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 9, 5, 14, 0, 0)));

        var dateInputs = cut.FindAll("input[type=datetime-local]");
        Assert.Equal("2026-09-01T08:00:00", dateInputs[0].GetAttribute("min"));
        Assert.Equal("2026-09-10T18:00:00", dateInputs[0].GetAttribute("max"));
        Assert.Equal("2026-09-01T08:00:00", dateInputs[1].GetAttribute("min"));
        Assert.Equal("2026-09-10T18:00:00", dateInputs[1].GetAttribute("max"));
    }

    [Fact]
    public void NewItem_StartOutsideLegWindow_IsPulledToTheLegStart()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));

        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 8, 20, 14, 0, 0)));

        var dateInputs = cut.FindAll("input[type=datetime-local]");
        Assert.Equal("2026-09-01T08:00:00", dateInputs[0].GetAttribute("value"));
        Assert.Equal("2026-09-01T08:00:00", dateInputs[1].GetAttribute("value"));
    }

    [Fact]
    public void StartOutsideLegWindow_ShowsValidationMessage()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));

        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 9, 5, 14, 0, 0)));

        cut.Find(".col-7 input").Change("Louvre tour");
        cut.FindAll("input[type=datetime-local]")[0].Change("2026-09-20T09:00");
        cut.Find("form").Submit();

        Assert.Contains("Start must be between", cut.Markup, StringComparison.Ordinal);
    }
}
