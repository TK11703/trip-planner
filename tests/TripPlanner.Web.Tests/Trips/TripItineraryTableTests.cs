using System.Diagnostics;
using Bunit;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class TripItineraryTableTests : TestContext
{
    [Fact]
    public void RendersCaptionAndSevenColumnHeaders()
    {
        var cut = Render(TripFixtures.Representative());

        var caption = cut.Find("caption");
        Assert.Equal("Itinerary", caption.TextContent.Trim());
        Assert.Contains("visually-hidden", caption.ClassList);
        Assert.Equal(
            new[] { "Type", "Title", "Location", "Start", "End", "Confirmation", "Est. Cost" },
            cut.FindAll("thead th").Select(header => header.TextContent.Trim()).ToArray());
    }

    [Fact]
    public void RendersChronologicalLegGroupsItemsAndEmptyLegs()
    {
        var cut = Render(TripFixtures.Representative());

        Assert.Equal(
            new[] { "Arrival", "Free day", "Departure", "Unassigned" },
            cut.FindAll("tr.tp-itinerary-leg .tp-itinerary-leg-title").Select(title => title.TextContent.Trim()).ToArray());
        Assert.All(cut.FindAll("tr.tp-itinerary-leg th"), header => Assert.Equal("7", header.GetAttribute("colspan")));
        Assert.Equal(
            new[] { "Walk", "Dinner", "Pack", "Review transfer" },
            cut.FindAll("tr.tp-itinerary-item").Select(row => row.QuerySelectorAll("td")[1].TextContent.Trim()).ToArray());
        Assert.Contains("No items for this leg.", cut.Markup, StringComparison.Ordinal);
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
        var cut = RenderComponent<TripItineraryTable>(parameters => parameters
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
        var cut = RenderComponent<TripItineraryTable>(parameters => parameters
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
    public void TableActionsInvokeCreateCallbacks()
    {
        var addLegCount = 0;
        Guid? initialLeg = TripFixtures.ArrivalLegId;
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());
        var cut = RenderComponent<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model)
            .Add(component => component.OnAddLeg, () => addLegCount++)
            .Add(component => component.OnAddItem, id => initialLeg = id));

        cut.Find("[aria-label='Add leg']").Click();
        cut.Find("[aria-label='Add item']").Click();

        Assert.Equal(1, addLegCount);
        Assert.Null(initialLeg);
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
        var cut = RenderComponent<TripItineraryTable>(parameters => parameters
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
        RenderComponent<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, TripPrintFormatting.BuildItineraryTable(trip)));
}