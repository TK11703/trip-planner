using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// User Story 1 (P1): A new event defaults its start to the active date and its end to +1h.
public class TrackedItemFormDateDefaultTests : TestContext
{
    public TrackedItemFormDateDefaultTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new FormStubTripApiClient());
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
    }

    [Fact]
    public void CreateForm_DefaultsStartToInitialDate_AndEndToOneHourLater()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var activeStart = new DateTime(2026, 9, 5, 14, 0, 0);

        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, activeStart));

        var dateInputs = cut.FindAll("input[type=datetime-local]");
        Assert.Equal("2026-09-05T14:00:00", dateInputs[0].GetAttribute("value"));
        Assert.Equal("2026-09-05T15:00:00", dateInputs[1].GetAttribute("value"));
    }
}
