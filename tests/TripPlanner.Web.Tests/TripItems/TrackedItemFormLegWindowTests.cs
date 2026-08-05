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

    // A leg is optional. With none selected there is no travel window, so the pickers are
    // unbounded and the window rule never fires.
    [Fact]
    public void NoLegSelected_LeavesDatePickersUnbounded()
    {
        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, Array.Empty<TripPlanner.Contracts.Trips.TripLegDto>()));

        var dateInputs = cut.FindAll("input[type=datetime-local]");
        Assert.True(string.IsNullOrEmpty(dateInputs[0].GetAttribute("min")));
        Assert.True(string.IsNullOrEmpty(dateInputs[0].GetAttribute("max")));
        Assert.True(string.IsNullOrEmpty(dateInputs[1].GetAttribute("min")));
        Assert.True(string.IsNullOrEmpty(dateInputs[1].GetAttribute("max")));
    }

    [Fact]
    public void NoLegSelected_StartFarFromAnyLeg_DoesNotBlockSubmit()
    {
        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, Array.Empty<TripPlanner.Contracts.Trips.TripLegDto>()));

        cut.Find(".col-7 input").Change("Museum pass");
        cut.FindAll("input[type=datetime-local]")[0].Change("2026-09-20T09:00");
        cut.Find("form").Submit();

        Assert.DoesNotContain("Start must be between", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Select the trip leg this item belongs to", cut.Markup, StringComparison.Ordinal);
    }

    // Feature 024, FR-013: an item that arrived without a leg can be related to one later, but
    // the leg's travel window is binding the moment it is chosen.
    [Fact]
    public void UnassignedItem_CanBeRelatedToACoveringLeg()
    {
        var covering = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var elsewhere = TrackedItemFormTestData.Leg(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 5, 18, 0, 0));
        var item = TrackedItemFormTestData.Item(null, new DateTime(2026, 9, 5, 14, 0, 0), new DateTime(2026, 9, 5, 16, 0, 0));

        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { covering, elsewhere })
            .Add(x => x.Item, item));

        cut.Find("#item-leg").Change(covering.TripLegId.ToString());
        cut.Find("form").Submit();

        Assert.DoesNotContain("Start must be between", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void UnassignedItem_IsRefusedAgainstALegThatDoesNotCoverIt()
    {
        var covering = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));
        var elsewhere = TrackedItemFormTestData.Leg(new DateTime(2026, 10, 1, 8, 0, 0), new DateTime(2026, 10, 5, 18, 0, 0));
        var item = TrackedItemFormTestData.Item(null, new DateTime(2026, 9, 5, 14, 0, 0), new DateTime(2026, 9, 5, 16, 0, 0));

        var cut = RenderComponent<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { covering, elsewhere })
            .Add(x => x.Item, item));

        cut.Find("#item-leg").Change(elsewhere.TripLegId.ToString());
        cut.Find("form").Submit();

        Assert.Contains("Start must be between", cut.Markup, StringComparison.Ordinal);
    }
}
