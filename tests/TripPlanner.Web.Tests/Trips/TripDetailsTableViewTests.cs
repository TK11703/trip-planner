using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.Infrastructure;
using TripPlanner.Web.Tests.TripItems;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class TripDetailsTableViewTests : BunitContext
{
    private bool _servicesConfigured;

    [Fact]
    public void TableAndTimelineAreStableDirectEntryLinks()
    {
        var table = RenderDetails();
        table.WaitForAssertion(() => Assert.NotEmpty(table.FindAll("table.tp-itinerary-table")));
        var timelineHref = ViewLink(table, "Timeline").GetAttribute("href");
        Assert.Contains("view=timeline", timelineHref, StringComparison.Ordinal);

        var timeline = RenderDetails("timeline", new DateOnly(2026, 7, 16));
        timeline.WaitForAssertion(() => Assert.Single(timeline.FindComponents<TripTimeline>()));
        Assert.DoesNotContain("table.tp-itinerary-table", timeline.Markup, StringComparison.Ordinal);
        Assert.Contains("view=table", ViewLink(timeline, "Table").GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void BreadcrumbLinksToHomeAndTripsAndMarksTripCurrent()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".breadcrumb .breadcrumb-item")));

        var crumbs = cut.FindAll(".breadcrumb .breadcrumb-item");
        Assert.Equal(3, crumbs.Count);
        Assert.Equal("/", crumbs[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("/trips", crumbs[1].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("page", crumbs[2].GetAttribute("aria-current"));
    }

    [Fact]
    public void TripActionsUseHrefLinksWhileDeleteAndMapRemainOverlays()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("header .btn")));
        var header = cut.Find("header");
        Assert.Contains("/edit?returnUrl=", header.QuerySelector("a.btn")!.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Contains("/share?returnUrl=", header.QuerySelector("a.btn-outline-primary")!.GetAttribute("href"), StringComparison.Ordinal);

        cut.Find("[aria-label='View all trip locations on a map']").Click();
        Assert.Single(cut.FindComponents<TripMapModal>());
        cut.Find(".modal [aria-label='Close']").Click();
        Assert.Empty(cut.FindComponents<TripMapModal>());

        cut.FindAll("header button").Single(button => button.TextContent.Contains("Delete trip", StringComparison.Ordinal)).Click();
        Assert.Contains("Delete trip", cut.Find(".modal-title").TextContent);
    }

    [Fact]
    public void AddActionsAndTableEditActionsAreRealEntryLinks()
    {
        var cut = RenderDetails();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("table.tp-itinerary-table")));

        var addLeg = cut.Find(".card-header [aria-label='Add leg']");
        var addItem = cut.Find(".card-header [aria-label='Add item']");
        Assert.StartsWith("/trips/", addLeg.GetAttribute("href"));
        Assert.Contains("/legs/new", addLeg.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Contains("/items/new", addItem.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Contains("returnUrl=", addItem.GetAttribute("href"), StringComparison.Ordinal);

        var editLeg = cut.Find("[aria-label='Edit leg Arrival']");
        Assert.Contains($"/legs/{TripFixtures.ArrivalLegId:D}/edit", editLeg.GetAttribute("href"), StringComparison.Ordinal);
        var editItem = cut.Find("[aria-label='Edit item Walk']");
        Assert.Contains($"/items/{TripFixtures.WalkItemId:D}/edit", editItem.GetAttribute("href"), StringComparison.Ordinal);
        var addToLeg = cut.Find("[aria-label='Add item to Arrival']");
        Assert.Contains($"legId={TripFixtures.ArrivalLegId:D}", addToLeg.GetAttribute("href"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TimelineEntryLinksCarryDateAndStableTargets()
    {
        var date = new DateOnly(2026, 7, 16);
        var cut = RenderDetails("timeline", date);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<TripTimeline>()));

        Assert.Contains("view=table", ViewLink(cut, "Table").GetAttribute("href"), StringComparison.Ordinal);
        var addLeg = cut.Find(".card-header [aria-label='Add leg']");
        var addItem = cut.Find(".card-header [aria-label='Add item']");
        Assert.Contains("date%3D2026-07-16", addLeg.GetAttribute("href"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("date%3D2026-07-16", addItem.GetAttribute("href"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("returnUrl=", addLeg.GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void UnassignedWarningRemainsAboveTheItineraryInBothViews()
    {
        foreach (var view in new string?[] { null, "timeline" })
        {
            var cut = RenderDetails(view);
            cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".alert-warning")));
            var warning = cut.Find(".alert-warning");
            Assert.Equal("alert", warning.GetAttribute("role"));
            Assert.Equal("Immediate Action Needed!", warning.QuerySelector(".alert-heading")!.TextContent.Trim());
            Assert.Contains("2 items are not related to a trip leg.", warning.TextContent, StringComparison.Ordinal);
            Assert.Contains("card", warning.NextElementSibling!.ClassList);
        }
    }

    private static AngleSharp.Dom.IElement ViewLink(IRenderedComponent<TripDetails> cut, string label) =>
        cut.FindAll("[aria-label='Itinerary view'] a").Single(anchor => anchor.TextContent.Trim() == label);

    private IRenderedComponent<TripDetails> RenderDetails(string? view = null, DateOnly? date = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var trip = TripFixtures.Representative();
        var api = new StubTripApiClient();
        api.Details[trip.TripId] = trip;
        if (!_servicesConfigured)
        {
            Services.AddSingleton<TripPlanner.Web.Features.Trips.ITripApiClient>(api);
            Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());
            Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider>(new StubMapPreferenceProvider());
            _servicesConfigured = true;
        }
        var query = new List<string>();
        if (view is not null) query.Add($"view={Uri.EscapeDataString(view)}");
        if (date is { } day) query.Add($"date={day:yyyy-MM-dd}");
        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo($"/trips/{trip.TripId:D}{suffix}");
        return Render<TripDetails>(parameters => parameters.Add(component => component.TripId, trip.TripId));
    }
}
