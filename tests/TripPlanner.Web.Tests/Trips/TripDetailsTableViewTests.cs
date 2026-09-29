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
    public void BreadcrumbLinksToTripsAndHostsTheViewModeToggleAtTheRight()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".tp-breadcrumb-row [role='group'][aria-label='Itinerary view']")));

        var crumbs = cut.FindAll(".breadcrumb .breadcrumb-item");
        Assert.Equal(2, crumbs.Count);
        Assert.Equal("/trips", crumbs[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("page", crumbs[1].GetAttribute("aria-current"));
        Assert.Contains("View mode:", cut.Find(".tp-breadcrumb-row").TextContent);
        Assert.Single(cut.FindAll("[role='group'][aria-label='Itinerary view']"));
    }

    [Fact]
    public void PageHeaderOrdersSmallActionsWithoutAnEditDropdown()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("header .btn")));

        var header = cut.Find("header");
        var actions = header.QuerySelectorAll("a.btn, button.btn").Select(e => e.TextContent.Trim()).ToArray();

        Assert.Equal(new[] { "Edit trip", "Print", "Share", "Delete trip" }, actions);
        Assert.All(header.QuerySelectorAll("a.btn, button.btn"), e => Assert.Contains("btn-sm", e.ClassList));
        Assert.Empty(header.QuerySelectorAll(".dropdown-toggle, .dropdown-menu, [aria-label='Itinerary view']"));
        Assert.Empty(cut.Find(".card-header").QuerySelectorAll("[aria-label='Itinerary view']"));
    }

    [Fact]
    public void CardHeaderRunsFromMapToTheDateNavigationInTimelineView()
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
        var stepIndex = IndexOfLabel("Step timeline by day");
        var jumpIndex = IndexOfLabel("Jump to a date in the trip");

        Assert.True(mapIndex >= 0, "View map should render in the card header.");
        Assert.True(mapIndex < stepIndex, "The day-step controls should follow View map.");
        Assert.True(stepIndex < jumpIndex, "Jump to date should follow the day-step controls.");
    }

    [Fact]
    public void AddActionsLeadTheCardHeaderInBothViews()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("table.tp-itinerary-table")));

        AssertAddActionsPrecedeMap(cut);
        Assert.NotNull(cut.Find(".card-body.tp-itinerary-flush table.tp-itinerary-table"));

        cut.Find(".card-header [aria-label='Add leg']").Click();
        Assert.Single(cut.FindComponents<TripLegForm>());
        cut.Find("[aria-label='Close']").Click();

        ViewButton(cut, "Timeline").Click();
        AssertAddActionsPrecedeMap(cut);
        Assert.Single(cut.FindAll(".card-body.tp-itinerary-flush"));

        cut.Find(".card-header [aria-label='Add item']").Click();
        Assert.Single(cut.FindComponents<TrackedItemForm>());
    }

    [Fact]
    public void UnassignedItemsWarningSitsAboveTheCardInBothViews()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("table.tp-itinerary-table")));

        AssertWarningPrecedesCard(cut);

        ViewButton(cut, "Timeline").Click();
        AssertWarningPrecedesCard(cut);
        Assert.Empty(cut.Find(".card-body").QuerySelectorAll(".alert-warning"));
    }

    private static void AssertWarningPrecedesCard(IRenderedComponent<TripDetails> cut)
    {
        var warning = cut.Find(".alert-warning");
        Assert.Equal("alert", warning.GetAttribute("role"));
        Assert.Equal("Immediate Action Needed!", warning.QuerySelector(".alert-heading")!.TextContent.Trim());
        Assert.Contains("2 items are not related to a trip leg.", warning.TextContent, StringComparison.Ordinal);
        Assert.Contains("card", warning.NextElementSibling!.ClassList);
    }

    private static void AssertAddActionsPrecedeMap(IRenderedComponent<TripDetails> cut)
    {
        var headerElements = cut.Find(".card-header").QuerySelectorAll("*").ToList();
        int IndexOfLabel(string label) => headerElements.FindIndex(e => e.GetAttribute("aria-label") == label);
        var addLegIndex = IndexOfLabel("Add leg");
        var addItemIndex = IndexOfLabel("Add item");
        var mapIndex = IndexOfLabel("View all trip locations on a map");

        Assert.True(addLegIndex >= 0 && addLegIndex < addItemIndex, "Add item should follow Add leg.");
        Assert.True(addItemIndex < mapIndex, "View map should follow the add actions.");
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
        cut.WaitForAssertion(() => cut.Find(".card-header [aria-label='Add leg']").Click());

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