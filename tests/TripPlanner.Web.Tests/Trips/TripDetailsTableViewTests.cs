using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
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

    [Theory]
    [InlineData(TripAccessLevel.Owner)]
    [InlineData(TripAccessLevel.Collaborator)]
    [InlineData(TripAccessLevel.Viewer)]
    public void OverviewPlacesAboutFirstAndDashboardWithAccessAndLinksEditableLegsToEdit(TripAccessLevel access)
    {
        var cut = RenderDetails("timeline", tab: "overview",
            tripOverride: TripFixtures.Representative() with { IsOwner = access == TripAccessLevel.Owner, AccessLevel = access });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-overview")));

        var overview = cut.Find(".trip-overview");
        var topRow = overview.FirstElementChild!;
        Assert.Contains("row", topRow.ClassList);
        Assert.NotNull(topRow.Children[0].QuerySelector(".trip-about"));
        Assert.Contains("col-lg-8", topRow.Children[0].ClassList);
        Assert.Contains("About this trip", topRow.Children[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("col-lg-4", topRow.Children[1].ClassList);
        Assert.Contains("Sharing", topRow.Children[1].TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Travel dates", topRow.TextContent, StringComparison.Ordinal);
        var contentRow = topRow.NextElementSibling!;
        var left = contentRow.QuerySelector(".col-lg-8")!;
        var right = contentRow.QuerySelector(".col-lg-4")!;
        Assert.NotNull(left.QuerySelector(".trip-leg-list"));
        Assert.Null(left.QuerySelector("[aria-label='Trip dashboard']"));
        var dashboard = right.QuerySelector("[aria-label='Trip dashboard']")!;
        Assert.NotNull(dashboard);
        Assert.Equal(new[] { "Legs", "Travel Items", "Estimated total" },
            dashboard.QuerySelectorAll("h2").Select(heading => heading.TextContent.Trim()));
        Assert.DoesNotContain("Sharing", right.TextContent, StringComparison.Ordinal);
        Assert.Contains("Travel Items", right.TextContent, StringComparison.Ordinal);
        if (access == TripAccessLevel.Viewer)
        {
            Assert.Empty(cut.FindAll(".trip-leg-list h3 a"));
            return;
        }
        foreach (var leg in TripFixtures.Representative().Legs)
        {
            var link = cut.Find($".trip-leg-list h3 a[href*='/legs/{leg.TripLegId}/edit']");
            var href = link.GetAttribute("href");
            Assert.Equal($"Edit travel leg: {link.TextContent}", link.GetAttribute("title"));
            Assert.Equal($"Edit leg {link.TextContent}", link.GetAttribute("aria-label"));
            Assert.Contains("view%3Dtimeline", href, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("tab%3Doverview", href, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OverviewDashboardUsesLargeIconsAndSharingKeepsSmallHeaderIcon()
    {
        var cut = RenderDetails(view: null);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-overview")));

        foreach (var title in new[] { "Legs", "Travel Items" })
        {
            var heading = Assert.Single(cut.FindAll(".trip-overview h2"),
                element => element.TextContent.Trim() == title);
            Assert.Null(heading.QuerySelector("svg"));
            var widget = heading.Closest(".trip-icon-widget");
            Assert.NotNull(widget);
            var icon = widget.QuerySelector(":scope > .trip-widget-icon > svg");
            Assert.NotNull(icon);
            Assert.NotEmpty(icon.Children);
            Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        }
        var sharing = Assert.Single(cut.FindAll(".trip-overview h2"),
            element => element.TextContent.Trim() == "Sharing");
        Assert.NotNull(sharing.QuerySelector("svg"));
        Assert.Null(sharing.Closest(".trip-icon-widget"));
        Assert.Empty(sharing.ParentElement!.QuerySelectorAll("a, button"));
        Assert.DoesNotContain("Select Manage", cut.Find(".trip-overview").TextContent, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("header [aria-label='Share trip']"));
        Assert.DoesNotContain("People with access", cut.Find(".trip-overview").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void OverviewLegsUseAccessibleModeIconsInsteadOfTextBadges()
    {
        var cut = RenderDetails(view: null);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-leg-list")));

        var legs = cut.FindAll(".trip-leg-list li");
        Assert.Equal(3, legs.Count);
        Assert.Equal(new[] { "Car", "Stay", "Flight" },
            legs.Select(leg => leg.QuerySelector(".trip-leg-mode")!.GetAttribute("aria-label")));
        foreach (var leg in legs)
        {
            var icon = leg.QuerySelector(".trip-leg-mode")!;
            Assert.Equal("img", icon.GetAttribute("role"));
            Assert.Equal(icon.GetAttribute("aria-label"), icon.GetAttribute("title"));
            Assert.NotNull(icon.QuerySelector("svg"));
            Assert.True(string.IsNullOrWhiteSpace(icon.TextContent));
            Assert.Null(leg.QuerySelector(".badge"));
            Assert.Contains("mb-1", leg.QuerySelector("h3")!.ParentElement!.ClassList);
        }
    }

    [Fact]
    public void OverviewShowsSharedPeopleAndTripSpecificEstimatedTotal()
    {
        var trip = TripFixtures.Representative() with
        {
            EstimatedCostTotal = 420m,
            SharedPeople =
            [
                new("owner", "Trip owner", "owner@example.test", TripAccessLevel.Owner, DateTimeOffset.UtcNow),
                new("collaborator", "Travel companion", "companion@example.test", TripAccessLevel.Collaborator, DateTimeOffset.UtcNow),
                new("viewer", null, "viewer@example.test", TripAccessLevel.Viewer, DateTimeOffset.UtcNow)
            ]
        };
        var cut = RenderDetails(view: null, tripOverride: trip);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-overview")));
        Assert.Contains("420", cut.Find("[aria-label='Trip dashboard']").TextContent, StringComparison.Ordinal);
        var members = cut.FindAll(".trip-overview .list-group-item");
        Assert.Equal(3, members.Count);
        Assert.Contains("Travel companion", members[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("Collaborator", members[1].TextContent, StringComparison.Ordinal);
        Assert.Contains("viewer@example.test", members[2].TextContent, StringComparison.Ordinal);
        Assert.Contains("Viewer", members[2].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void PlainTripUrlOpensOverviewWithTitleDatesAndTripDashboard()
    {
        var cut = RenderDetails(view: null);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-overview")));

        var header = cut.Find("header");
        Assert.Equal("Japan 2026", header.QuerySelector("h1")!.TextContent);
        var trip = TripFixtures.Representative();
        Assert.Equal($"{trip.StartDate:MMM d, yyyy} \u2013 {trip.EndDate:MMM d, yyyy}",
            header.QuerySelector(".entry-page-subtitle")!.TextContent);
        Assert.DoesNotContain("Travel dates", cut.Find(".trip-overview").TextContent, StringComparison.Ordinal);
        Assert.Contains("Cherry blossoms", cut.Find(".trip-overview").TextContent, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[aria-label='Trip dashboard']"));
        Assert.Equal(new[] { "Arrival", "Free day", "Departure" },
            cut.FindAll(".trip-leg-list h3").Select(heading => heading.TextContent.Trim()));
        Assert.Contains("Sharing", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".trip-overview .alert-warning"));
        Assert.DoesNotContain("Review unassigned items", cut.Find(".trip-overview").TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[aria-label='Itinerary view'], table, .ttl-scroll"));
        Assert.Contains("tab%3Doverview", cut.Find("[aria-label='Add leg']").GetAttribute("href"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("page", cut.Find("[aria-label='Trip sections'] a").GetAttribute("aria-current"));
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("details")]
    public void TripDateSubtitleAppearsOnBothTabs(string tab)
    {
        var trip = TripFixtures.Representative();
        var cut = RenderDetails(tab: tab, tripOverride: trip);
        cut.WaitForAssertion(() => Assert.Equal(
            $"{trip.StartDate:MMM d, yyyy} \u2013 {trip.EndDate:MMM d, yyyy}",
            cut.Find("header .entry-page-subtitle").TextContent));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OverviewShowsMissingDescriptionAndEmptyLegGuidance(string? description)
    {
        var cut = RenderDetails(view: null, tripOverride: TripFixtures.Empty() with { Description = description });
        cut.WaitForAssertion(() => Assert.Contains("A description was not provided.", cut.Markup, StringComparison.Ordinal));
        Assert.Contains("No trip legs yet.", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("[aria-label='Add leg']"));
    }

    [Theory]
    [InlineData(TripAccessLevel.Viewer, false)]
    [InlineData(TripAccessLevel.Collaborator, true)]
    public void OverviewPreservesSharedTripPermissions(TripAccessLevel access, bool canEdit)
    {
        var cut = RenderDetails(view: null, tripOverride: TripFixtures.Representative() with { IsOwner = false, AccessLevel = access });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-overview")));
        Assert.Equal(canEdit, cut.FindAll("[aria-label='Add leg']").Count == 1);
        Assert.Equal(canEdit, cut.FindAll("header [aria-label='Edit trip']").Count == 1);
        Assert.Empty(cut.FindAll("header button"));
        Assert.Empty(cut.FindAll("header [aria-label='Print trip'], header [aria-label='Share trip']"));
    }

    [Fact]
    public void ExplicitOverviewKeepsSelectedFormatButHidesFormatToggle()
    {
        var cut = RenderDetails("timeline", new DateOnly(2026, 7, 16), tab: "overview");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-overview")));
        Assert.Empty(cut.FindAll("[aria-label='Itinerary view']"));
        var detailsLink = cut.FindAll("[aria-label='Trip sections'] a").Single(a => a.TextContent == "Details");
        Assert.Contains("view=timeline", detailsLink.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Contains("date=2026-07-16", detailsLink.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Contains("tab=details", detailsLink.GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitDetailsOpensGroupedTablesWithoutOverviewContent()
    {
        var cut = RenderDetails(view: null, tab: "details");
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("table.tp-itinerary-table").Count));
        Assert.Empty(cut.FindAll(".trip-overview"));
        Assert.NotNull(cut.Find("[aria-label='Trip details'] [aria-label='Itinerary view']"));
        Assert.All(cut.FindAll("table"), table => Assert.Equal(6, table.QuerySelectorAll("col").Length));
    }

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
        Assert.Equal(new[] { "Edit trip", "Delete trip", "Print trip", "Share trip" },
            header.QuerySelectorAll(".btn").Select(button => button.GetAttribute("aria-label")));
        Assert.All(header.QuerySelectorAll(".btn"), button =>
        {
            Assert.Equal(button.GetAttribute("aria-label"), button.GetAttribute("title"));
            Assert.Equal(string.Empty, button.TextContent.Trim());
            Assert.NotNull(button.QuerySelector("svg[aria-hidden='true']"));
        });
        Assert.Contains("/edit?returnUrl=", header.QuerySelector("a.btn")!.GetAttribute("href"), StringComparison.Ordinal);
        Assert.Contains("/share?returnUrl=", header.QuerySelector("a.btn-outline-primary")!.GetAttribute("href"), StringComparison.Ordinal);

        cut.Find("[aria-label='View all trip locations on a map']").Click();
        Assert.Single(cut.FindComponents<TripMapModal>());
        cut.Find(".modal [aria-label='Close']").Click();
        Assert.Empty(cut.FindComponents<TripMapModal>());

        cut.Find("header [aria-label='Delete trip']").Click();
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
        foreach (var view in new[] { "table", "timeline" })
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

    private IRenderedComponent<TripDetails> RenderDetails(string? view = "table", DateOnly? date = null, string? tab = null,
        TripDetail? tripOverride = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var trip = tripOverride ?? TripFixtures.Representative();
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
        if (tab is not null) query.Add($"tab={tab}");
        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo($"/trips/{trip.TripId:D}{suffix}");
        return Render<TripDetails>(parameters => parameters.Add(component => component.TripId, trip.TripId));
    }
}
