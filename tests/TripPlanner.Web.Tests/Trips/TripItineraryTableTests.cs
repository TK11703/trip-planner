using System.Diagnostics;
using Bunit;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class TripItineraryTableTests : BunitContext
{
    [Fact]
    public void GroupedUnassignedItemsHaveWarningHeaderAndIconsOnlyOnUnassignedRows()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.GroupByLeg, true));

        Assert.Contains("tp-itinerary-unassigned-header", cut.Find("#unassigned-items").ClassList);
        Assert.NotNull(cut.Find("#unassigned-items svg"));
        Assert.Equal(model.UnassignedItems.Count, cut.FindAll(".tp-itinerary-item-warning").Count);
        foreach (var item in model.UnassignedItems)
        {
            var row = cut.Find($"#item-{item.TrackedItemId}");
            Assert.NotNull(row.QuerySelector("[aria-label='Travel leg required'] svg"));
            Assert.Null(row.QuerySelector(".tp-tracked-item-icon"));
        }
        Assert.All(cut.FindAll(".tp-itinerary-item:not(.tp-itinerary-item-warning)"), row =>
            Assert.Null(row.QuerySelector(".tp-itinerary-assignment-warning")));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "1 item")]
    [InlineData(2, "2 items")]
    public void GroupedHeaderShowsCountOnlyForNonemptyLegs(int count, string? expected)
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var leg = model.Legs[0] with { Items = model.Legs[0].Items.Take(count).ToArray() };
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, new ItineraryTableModel([leg], []))
            .Add(component => component.GroupByLeg, true));

        var counts = cut.FindAll(".tp-itinerary-leg-count");
        if (expected is null) Assert.Empty(counts);
        else Assert.Equal(expected, Assert.Single(counts).TextContent);
    }

    [Fact]
    public void HeaderToggleIsSeparateFromEditAndAddActions()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        Guid? edited = null;
        Guid? added = null;
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.GroupByLeg, true)
            .Add(component => component.OnEditLeg, id => edited = id)
            .Add(component => component.OnAddItem, id => added = id));

        var header = cut.Find($"#leg-{TripFixtures.ArrivalLegId}");
        Assert.NotNull(header.QuerySelector(".tp-itinerary-leg-expandable .tp-itinerary-leg-toggle"));
        var edit = cut.Find($"#{TripItineraryTable.EditLegActionId(TripFixtures.ArrivalLegId)}");
        Assert.Equal("Edit travel leg: Arrival", edit.GetAttribute("title"));
        edit.Click();
        cut.Find($"#{TripItineraryTable.AddItemActionId(TripFixtures.ArrivalLegId)}").Click();
        Assert.Equal(TripFixtures.ArrivalLegId, edited);
        Assert.Equal(TripFixtures.ArrivalLegId, added);
        Assert.Equal("true", cut.Find($"#leg-{TripFixtures.ArrivalLegId} .tp-itinerary-leg-toggle").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void GroupedLegHeaderSeparatesIdentityMetadataAndAddItemAction()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.GroupByLeg, true)
            .Add(component => component.TripId, TripFixtures.RepresentativeTripId)
            .Add(component => component.CanEdit, true));

        var header = cut.Find($"#leg-{TripFixtures.ArrivalLegId}");
        Assert.Equal("Arrival", header.QuerySelector(".tp-itinerary-leg-identity .tp-itinerary-leg-title")!.TextContent);
        Assert.Equal("Edit travel leg: Arrival", header.QuerySelector("a.tp-itinerary-leg-title")!.GetAttribute("title"));
        Assert.Equal("Car", header.QuerySelector(".tp-itinerary-leg-icon")!.GetAttribute("aria-label"));
        Assert.Null(header.QuerySelector(".tp-itinerary-leg-mode"));
        var identity = header.QuerySelector(".tp-itinerary-leg-identity")!;
        Assert.Contains("tp-itinerary-leg-icon", identity.FirstElementChild!.ClassList);
        Assert.Contains("tp-itinerary-leg-title", identity.FirstElementChild.NextElementSibling!.ClassList);
        var details = header.QuerySelector(".tp-itinerary-leg-details")!;
        Assert.Contains("tp-itinerary-leg-count", details.NextElementSibling!.ClassList);
        Assert.Contains("tp-itinerary-row-add", details.NextElementSibling.NextElementSibling!.ClassList);
        var stay = cut.Find($"#leg-{model.Legs.Single(leg => leg.Title == "Free day").TripLegId}");
        Assert.Equal("Stay", stay.QuerySelector(".tp-itinerary-leg-icon")!.GetAttribute("aria-label"));
        Assert.NotNull(stay.QuerySelector(".tp-itinerary-leg-icon svg circle"));
        Assert.Contains("Seattle", header.QuerySelector(".tp-itinerary-leg-details .tp-itinerary-leg-route")!.TextContent, StringComparison.Ordinal);
        var add = header.QuerySelector(".tp-itinerary-leg-heading > a[aria-label='Add item to Arrival']")!;
        Assert.Contains("btn-link", add.ClassList);
        Assert.DoesNotContain("ttl-leg-add", add.ClassList);
        Assert.Empty(add.QuerySelectorAll("span:not([aria-hidden='true'])"));
        Assert.Equal("Add an item to Arrival", add.GetAttribute("title"));
        Assert.Contains($"legId={TripFixtures.ArrivalLegId}", add.GetAttribute("href"), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindAll("[aria-label='Add item to Departure']"));
    }

    [Fact]
    public void GroupedViewRendersTablesForItemCapableLegsAndUnassignedWithIdenticalColumnTracks()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.GroupByLeg, true));

        var tables = cut.FindAll("table");
        Assert.Equal(3, tables.Count);
        var tracks = tables[0].QuerySelector("colgroup")!.InnerHtml;
        Assert.All(tables, table =>
        {
            Assert.Equal(tracks, table.QuerySelector("colgroup")!.InnerHtml);
            Assert.Equal(6, table.QuerySelectorAll("thead th").Length);
            Assert.Empty(table.QuerySelectorAll("tbody .tp-itinerary-leg"));
            Assert.Equal("CAPTION", table.FirstElementChild!.TagName);
            Assert.Contains("visually-hidden", table.FirstElementChild.ClassList);
            Assert.Equal("THEAD", table.QuerySelector("colgroup")!.NextElementSibling!.TagName);
        });
        Assert.Equal(new[] { "Arrival", "Free day", "Departure", "Unassigned" },
            cut.FindAll(".tp-itinerary-leg-header .tp-itinerary-leg-title").Select(title => title.TextContent));
        Assert.Equal(4, cut.FindAll(".tp-itinerary-item").Count);
        Assert.Single(cut.FindAll(".tp-itinerary-leg-empty"));
        Assert.Single(cut.FindAll("[role='region']"));
    }

    [Fact]
    public void GroupedLegsToggleIndependentlyAndTravelOnlyLegsHaveNoToggle()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.GroupByLeg, true));

        var toggles = cut.FindAll(".accordion-button");
        Assert.Equal(model.Legs.Count(leg => leg.CanContainItems), toggles.Count);
        Assert.Empty(cut.FindAll($"#leg-{TripFixtures.DepartureLegId} .accordion-button"));
        var departure = cut.Find($"#leg-{TripFixtures.DepartureLegId}").ParentElement!;
        Assert.Single(departure.Children);
        Assert.Empty(departure.QuerySelectorAll("table, .accordion-collapse"));
        Assert.DoesNotContain("No items for this leg.", departure.TextContent, StringComparison.Ordinal);
        Assert.All(toggles, toggle => Assert.Equal("true", toggle.GetAttribute("aria-expanded")));

        var firstPanelId = toggles[0].GetAttribute("aria-controls");
        toggles[0].Click();
        Assert.Equal("false", cut.Find($"[aria-controls='{firstPanelId}']").GetAttribute("aria-expanded"));
        Assert.DoesNotContain("show", cut.Find($"#{firstPanelId}").ClassList);
        Assert.Equal("true", cut.FindAll(".accordion-button")[1].GetAttribute("aria-expanded"));

        cut.Find($"[aria-controls='{firstPanelId}']").Click();
        Assert.Contains("show", cut.Find($"#{firstPanelId}").ClassList);
    }

    [Fact]
    public void RendersCaptionAndSixColumnHeaders()
    {
        var cut = Render(TripFixtures.Representative());

        var caption = cut.Find("caption");
        Assert.Equal("Itinerary", caption.TextContent.Trim());
        Assert.Contains("visually-hidden", caption.ClassList);
        Assert.Equal(
            new[] { "Title", "Location", "Start", "End", "Confirmation", "Est. Cost" },
            cut.FindAll("thead th").Select(header => header.TextContent.Trim()).ToArray());
    }

    [Fact]
    public void RendersChronologicalLegGroupsItemsAndEmptyLegs()
    {
        var cut = Render(TripFixtures.Representative());

        Assert.Equal(
            new[] { "Arrival", "Free day", "Departure", "Unassigned" },
            cut.FindAll("tr.tp-itinerary-leg .tp-itinerary-leg-title").Select(title => title.TextContent.Trim()).ToArray());
        Assert.All(cut.FindAll("tr.tp-itinerary-leg th"), header => Assert.Equal("6", header.GetAttribute("colspan")));
        Assert.All(cut.FindAll("tr.tp-itinerary-leg-empty td"), cell => Assert.Equal("6", cell.GetAttribute("colspan")));
        Assert.Equal(
            new[] { "Walk", "Dinner", "Pack", "Review transfer" },
            cut.FindAll(".tp-itinerary-item-title > span").Select(title => title.TextContent.Trim()).ToArray());
        Assert.Contains("No items for this leg.", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TitleCellRendersMatchingLabeledIconsBeforeAssignedAndUnassignedItemTitles(bool interactive)
    {
        var trip = TripFixtures.Representative();
        var types = new[] { "reservation", "activity", "reminder", "event" };
        trip = trip with
        {
            TrackedItems = trip.TrackedItems.Select((item, index) => item with { ItemType = types[index] }).ToArray()
        };
        var cut = Render<TripItineraryTable>(parameters =>
        {
            parameters.Add(component => component.Model, TripPrintFormatting.BuildItineraryTable(trip));
            if (interactive)
            {
                parameters.Add(component => component.OnEditItem, _ => { });
            }
        });

        foreach (var row in cut.FindAll("tr.tp-itinerary-item"))
        {
            var cells = row.QuerySelectorAll("td");
            Assert.Equal(6, cells.Length);
            var title = cells[0].QuerySelector(".tp-itinerary-item-title")!;
            var item = trip.TrackedItems.Single(item => item.Title == title.Children[1].TextContent.Trim());
            var expectedLabel = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(item.ItemType);
            var icon = cells[0].QuerySelector("svg")!;
            var expectedIcon = Render<TrackedItemIcon>(p => p
                .Add(x => x.Type, item.ItemType)
                .Add(x => x.Label, expectedLabel));

            Assert.Equal(expectedLabel, icon.GetAttribute("aria-label"));
            Assert.False(icon.HasAttribute("aria-hidden"));
            Assert.Equal(expectedLabel, icon.QuerySelector("title")!.TextContent);
            Assert.Equal(expectedIcon.Find("svg").InnerHtml, icon.InnerHtml);
            Assert.Equal(icon, title.Children[0]);
            Assert.Equal(interactive ? "BUTTON" : "SPAN", title.Children[1].TagName);
        }
        Assert.Equal(4, cut.FindAll("tr.tp-itinerary-item svg").Count);
    }

    [Fact]
    public void EmptyTripRendersSharedEmptyStateWithoutTable()
    {
        var cut = Render(TripFixtures.Empty());

        Assert.Empty(cut.FindAll("table"));
        Assert.Contains("no legs or items", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EmptyTripOffersOnlyTheLegActionInAPromptedEmptyState()
    {
        var addLegCount = 0;
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Empty());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.OnAddLeg, () => addLegCount++)
            .Add(component => component.OnAddItem, _ => { }));

        Assert.NotNull(cut.Find(".border-dashed"));
        Assert.Empty(cut.FindAll("[aria-label='Add item']"));

        var addLeg = cut.Find("[aria-label='Add leg']");
        Assert.Contains("ttl-leg-add", addLeg.ClassList);
        addLeg.Click();

        Assert.Equal(1, addLegCount);
    }

    [Fact]
    public void InteractiveCallbacksReceiveStableIds()
    {
        Guid? editedLeg = null;
        Guid? editedItem = null;
        Guid? initialLeg = null;
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.OnEditLeg, id => editedLeg = id)
            .Add(component => component.OnEditItem, id => editedItem = id)
            .Add(component => component.OnAddItem, id => initialLeg = id));

        cut.Find("[aria-label='Edit leg Arrival']").Click();
        cut.Find("[aria-label='Edit item Walk']").Click();
        cut.Find("[aria-label='Add item to Arrival']").Click();

        Assert.Equal(TripFixtures.ArrivalLegId, editedLeg);
        Assert.Equal(TripFixtures.WalkItemId, editedItem);
        Assert.Equal(TripFixtures.ArrivalLegId, initialLeg);
        Assert.Empty(cut.FindAll("[aria-label='Add item to Departure']"));
    }

    [Fact]
    public void PopulatedTableLeavesTripLevelCreateActionsToTheHost()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.OnAddLeg, () => { })
            .Add(component => component.OnAddItem, _ => { }));

        Assert.Empty(cut.FindAll("[aria-label='Add leg']"));
        Assert.Empty(cut.FindAll("[aria-label='Add item']"));
        Assert.NotEmpty(cut.FindAll("[aria-label='Add item to Arrival']"));
    }

    [Fact]
    public void MissingCallbacksRenderNoInteractiveControls()
    {
        var cut = Render(TripFixtures.Representative());

        Assert.Empty(cut.FindAll("button"));
        Assert.Empty(cut.FindAll("input, select, textarea"));
    }

    [Fact]
    public void UsesNativeTableSemanticsAndLabeledOverflowRegion()
    {
        var cut = Render(TripFixtures.Representative());

        Assert.All(cut.FindAll("thead th"), header => Assert.Equal("col", header.GetAttribute("scope")));
        Assert.All(cut.FindAll("tr.tp-itinerary-leg th"), header => Assert.Equal("rowgroup", header.GetAttribute("scope")));
        var region = cut.Find(".tp-itinerary-table-region");
        Assert.Equal("region", region.GetAttribute("role"));
        Assert.Equal("Itinerary table", region.GetAttribute("aria-label"));
        Assert.Equal("0", region.GetAttribute("tabindex"));
    }

    [Fact]
    public void InteractiveActionsAreContextuallyNamedNativeButtons()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.OnEditLeg, _ => { })
            .Add(component => component.OnEditItem, _ => { })
            .Add(component => component.OnAddItem, _ => { }));

        Assert.Equal("BUTTON", cut.Find("[aria-label='Edit leg Arrival']").TagName);
        Assert.Equal("BUTTON", cut.Find("[aria-label='Edit item Walk']").TagName);
        Assert.Equal("BUTTON", cut.Find("[aria-label='Add item to Arrival']").TagName);
        Assert.Equal("Arrival", cut.Find("[aria-label='Edit leg Arrival']").TextContent.Trim());
        Assert.Equal("Walk", cut.Find("[aria-label='Edit item Walk']").TextContent.Trim());
        Assert.DoesNotContain(cut.FindAll("button"), button => button.TextContent.Trim() == "Edit");
    }

    [Fact]
    public void RepresentativeLargeTripProjectsAndRendersWithoutPerceptibleDelay()
    {
        var source = TripFixtures.Representative();
        var legTemplate = source.Legs[0];
        var itemTemplate = source.TrackedItems[0];
        var legs = Enumerable.Range(0, 25)
            .Select(index => legTemplate with
            {
                TripLegId = Guid.NewGuid(),
                Title = $"Leg {index + 1}",
                StartLocal = legTemplate.StartLocal.AddDays(index),
                EndLocal = legTemplate.EndLocal.AddDays(index),
                SortOrder = index
            })
            .ToArray();
        var items = legs.SelectMany((leg, legIndex) => Enumerable.Range(0, 10)
            .Select(itemIndex => itemTemplate with
            {
                TrackedItemId = Guid.NewGuid(),
                TripLegId = leg.TripLegId,
                Title = $"Item {legIndex + 1}-{itemIndex + 1}",
                StartLocal = leg.StartLocal.AddMinutes(itemIndex * 10),
                StartsAt = new DateTimeOffset(leg.StartLocal.AddMinutes(itemIndex * 10), TimeSpan.FromHours(-4)),
                SortOrder = itemIndex
            }))
            .ToArray();
        var trip = source with { Legs = legs, TrackedItems = items };

        var stopwatch = Stopwatch.StartNew();
        var cut = Render(trip);
        stopwatch.Stop();

        Assert.Equal(25, cut.FindAll("tr.tp-itinerary-leg").Count);
        Assert.Equal(250, cut.FindAll("tr.tp-itinerary-item").Count);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Projection and render took {stopwatch.Elapsed}.");
    }

    private IRenderedComponent<TripItineraryTable> Render(TripPlanner.Contracts.Trips.TripDetail trip) =>
        Render<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, TripPrintFormatting.BuildItineraryTable(trip)));
}