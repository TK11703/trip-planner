using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Components.TripItems;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.Infrastructure;
using TripPlanner.Web.Tests.TripItems;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class TripDetailsTableViewTests : TestContext
{
    [Fact]
    public void DefaultsToTimelineAndSwitchesToTableInPlace()
    {
        var cut = RenderDetails();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindComponents<TripTimeline>());
            Assert.True(ViewButton(cut, "Timeline").HasAttribute("aria-pressed"));
        });

        ViewButton(cut, "Table").Click();

        Assert.Empty(cut.FindComponents<TripTimeline>());
        Assert.NotEmpty(cut.FindAll("table.tp-itinerary-table"));
        Assert.True(ViewButton(cut, "Table").HasAttribute("aria-pressed"));
    }

    [Fact]
    public void TableViewKeepsMapAndSuppressesTimelineDateControls()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => ViewButton(cut, "Table").Click());

        Assert.Single(cut.FindAll("[aria-label='View all trip locations on a map']"));
        Assert.Empty(cut.FindAll("[aria-label='Step timeline by day']"));
        Assert.Empty(cut.FindAll("[aria-label='Jump to a date in the trip']"));
    }

    [Fact]
    public void TableEditActionsOpenExistingForms()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => ViewButton(cut, "Table").Click());

        cut.Find("[aria-label='Edit leg Arrival']").Click();
        Assert.Single(cut.FindComponents<TripLegForm>());
        Assert.Contains("Edit leg", cut.Find(".modal-title").TextContent, StringComparison.Ordinal);

        cut.Find("[aria-label='Close']").Click();
        cut.Find("[aria-label='Edit item Walk']").Click();
        Assert.Single(cut.FindComponents<TrackedItemForm>());
        Assert.Contains("Edit item", cut.Find(".modal-title").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void LegSpecificAddUsesExistingFormWithInitialLeg()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => ViewButton(cut, "Table").Click());

        cut.Find("[aria-label='Add item to Arrival']").Click();

        var form = cut.FindComponent<TrackedItemForm>();
        Assert.Equal(TripFixtures.ArrivalLegId, form.Instance.InitialTripLegId);
    }

    [Fact]
    public void AddLegUsesTheTripDateRange()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => cut.FindAll("button").Single(button => button.TextContent.Trim() == "Add trip leg").Click());

        var form = cut.FindComponent<TripLegForm>();

        Assert.Equal(new DateOnly(2026, 7, 14), form.Instance.TripStartDate);
        Assert.Equal(new DateOnly(2026, 7, 20), form.Instance.TripEndDate);
    }

    [Fact]
    public void ClosingTableModalRestoresFocusToInvokingAction()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => ViewButton(cut, "Table").Click());

        cut.Find("[aria-label='Edit item Walk']").Click();
        cut.Find("[aria-label='Close']").Click();

        var invocation = JSInterop.VerifyInvoke("tripFocus.focusById");
        Assert.Equal(TripItineraryTable.EditItemActionId(TripFixtures.WalkItemId), invocation.Arguments[0]);
    }

    [Fact]
    public async Task SuccessfulSaveKeepsTableSelectedAndReloadsData()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => ViewButton(cut, "Table").Click());
        cut.Find("[aria-label='Add item']").Click();
        var form = cut.FindComponent<TrackedItemForm>();

        await cut.InvokeAsync(() => form.Instance.OnSaved.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("table.tp-itinerary-table"));
            Assert.True(ViewButton(cut, "Table").HasAttribute("aria-pressed"));
            Assert.Empty(cut.FindAll(".modal"));
        });
    }

    private IRenderedComponent<TripDetails> RenderDetails()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var trip = TripFixtures.Representative();
        var api = new StubTripApiClient();
        api.Details[trip.TripId] = trip;
        Services.AddSingleton<TripPlanner.Web.Features.Trips.ITripApiClient>(api);
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
        return RenderComponent<TripDetails>(parameters => parameters.Add(component => component.TripId, trip.TripId));
    }

    private static AngleSharp.Dom.IElement ViewButton(IRenderedComponent<TripDetails> cut, string label) =>
        cut.FindAll("[role='group'][aria-label='Itinerary view'] button")
            .Single(button => button.TextContent.Trim() == label);
}