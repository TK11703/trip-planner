using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.TripItems;

// An item belongs to a trip leg, so its date pickers are bounded to that leg's travel window.
public class TrackedItemFormLegWindowTests : BunitContext
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

        var cut = Render<TrackedItemForm>(p => p
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
    public void DatePickers_WithSecondBearingBounds_OfferWholeMinutesOnly()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 8, 10, 20, 39, 39), new DateTime(2026, 8, 13, 8, 5, 39));

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { leg })
            .Add(x => x.InitialTripLegId, leg.TripLegId)
            .Add(x => x.InitialStartsAt, new DateTime(2026, 8, 10, 21, 0, 0)));

        var dateInputs = cut.FindAll("input[type=datetime-local]");
        Assert.All(dateInputs, input => Assert.Equal("60", input.GetAttribute("step")));
        // Pulled inward to the next whole minute so the bound stays inside the leg's window.
        Assert.Equal("2026-08-10T20:40:00", dateInputs[0].GetAttribute("min"));
        Assert.Equal("2026-08-13T08:05:00", dateInputs[0].GetAttribute("max"));
    }

    [Fact]
    public void NewItem_StartOutsideLegWindow_IsPulledToTheLegStart()
    {
        var leg = TrackedItemFormTestData.Leg(new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 10, 18, 0, 0));

        var cut = Render<TrackedItemForm>(p => p
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

        var cut = Render<TrackedItemForm>(p => p
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
        var cut = Render<TrackedItemForm>(p => p
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
        var cut = Render<TrackedItemForm>(p => p
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

        var cut = Render<TrackedItemForm>(p => p
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

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { covering, elsewhere })
            .Add(x => x.Item, item));

        cut.Find("#item-leg").Change(elsewhere.TripLegId.ToString());
        cut.Find("form").Submit();

        Assert.Contains("Start must be between", cut.Markup, StringComparison.Ordinal);
    }

    // --- Feature 025: the leg list only offers legs that can actually hold an item ---

    private static readonly DateTime WindowStart = new(2026, 9, 1, 8, 0, 0);
    private static readonly DateTime WindowEnd = new(2026, 9, 10, 18, 0, 0);

    private static TripLegDto EligibleLeg(string title = "Hotel Kabuki")
        => TrackedItemFormTestData.Leg(WindowStart, WindowEnd, TripLegKinds.Stay, title: title);

    private static TripLegDto RestrictedLeg(string mode, string title = "Getting there")
        => TrackedItemFormTestData.Leg(WindowStart, WindowEnd, TripLegKinds.Travel, mode, title);

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    public void RestrictedLegs_AreNotOffered(string mode)
    {
        var stay = EligibleLeg();
        var restricted = RestrictedLeg(mode);

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { stay, restricted }));

        var values = cut.FindAll("#item-leg option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
        Assert.Contains(stay.TripLegId.ToString(), values);
        Assert.DoesNotContain(restricted.TripLegId.ToString(), values);
    }

    [Fact]
    public void StayAndCarLegs_AreBothOffered()
    {
        var stay = EligibleLeg();
        var car = RestrictedLeg(TransportationModes.Car, "Road trip");

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { stay, car }));

        var values = cut.FindAll("#item-leg option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
        Assert.Contains(stay.TripLegId.ToString(), values);
        Assert.Contains(car.TripLegId.ToString(), values);
    }

    /// <summary>
    /// A trip made only of flights has nowhere to put an item, and the form says so rather than
    /// offering a leg the API would refuse.
    /// </summary>
    [Fact]
    public void TripWithOnlyRestrictedLegs_ShowsTheEmptyState()
    {
        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { RestrictedLeg(TransportationModes.Flight), RestrictedLeg(TransportationModes.Train, "Onward") }));

        Assert.Contains("Add a stay or car leg first", cut.Markup, StringComparison.Ordinal);
        var values = cut.FindAll("#item-leg option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
        Assert.Equal(new[] { string.Empty }, values);
    }

    /// <summary>
    /// An item already sitting on a restricted leg keeps that leg visible so the traveler can move
    /// it deliberately instead of losing the relationship without noticing.
    /// </summary>
    [Fact]
    public void ItemAlreadyOnARestrictedLeg_StillSeesThatLeg()
    {
        var restricted = RestrictedLeg(TransportationModes.Flight);
        var stay = EligibleLeg();
        var item = TrackedItemFormTestData.Item(restricted.TripLegId, new DateTime(2026, 9, 5, 14, 0, 0), new DateTime(2026, 9, 5, 16, 0, 0));

        var cut = Render<TrackedItemForm>(p => p
            .Add(x => x.TripId, Guid.NewGuid())
            .Add(x => x.Legs, new[] { stay, restricted })
            .Add(x => x.Item, item));

        var values = cut.FindAll("#item-leg option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
        Assert.Contains(restricted.TripLegId.ToString(), values);
        Assert.Contains(stay.TripLegId.ToString(), values);
    }
}
