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

public class TripDetailsTableViewTests : BunitContext
{
    [Fact]
    public void DefaultsToTableAndSwitchesToTimelineInPlace()
    {
        var cut = RenderDetails();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("table.tp-itinerary-table"));
            Assert.True(ViewButton(cut, "Table").HasAttribute("aria-pressed"));
        });

        ViewButton(cut, "Timeline").Click();

        Assert.Single(cut.FindComponents<TripTimeline>());
        Assert.Empty(cut.FindAll("table.tp-itinerary-table"));
        Assert.True(ViewButton(cut, "Timeline").HasAttribute("aria-pressed"));
    }

    [Fact]
    public void HeaderControlsRunFromMapThroughTheToggleToTheDateNavigation()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[role='group'][aria-label='Itinerary view']")));

        var toggleLabels = cut.FindAll("[role='group'][aria-label='Itinerary view'] button")
            .Select(button => button.TextContent.Trim())
            .ToArray();
        Assert.Equal(new[] { "Table", "Timeline" }, toggleLabels);

        ViewButton(cut, "Timeline").Click();

        var headerElements = cut.Find(".card-header").QuerySelectorAll("*").ToList();
        int IndexOfLabel(string label) => headerElements.FindIndex(e => e.GetAttribute("aria-label") == label);

        var mapIndex = IndexOfLabel("View all trip locations on a map");
        var toggleIndex = IndexOfLabel("Itinerary view");
        var stepIndex = IndexOfLabel("Step timeline by day");
        var jumpIndex = IndexOfLabel("Jump to a date in the trip");

        Assert.True(mapIndex >= 0, "View map should render in the header.");
        Assert.True(mapIndex < toggleIndex, "View map should precede the itinerary view toggle.");
        Assert.True(toggleIndex < stepIndex, "The day-step controls should follow the itinerary view toggle.");
        Assert.True(stepIndex < jumpIndex, "Jump to date should follow the day-step controls.");
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
        return Render<TripDetails>(parameters => parameters.Add(component => component.TripId, trip.TripId));
    }

    private static AngleSharp.Dom.IElement ViewButton(IRenderedComponent<TripDetails> cut, string label) =>
        cut.FindAll("[role='group'][aria-label='Itinerary view'] button")
            .Single(button => button.TextContent.Trim() == label);
}