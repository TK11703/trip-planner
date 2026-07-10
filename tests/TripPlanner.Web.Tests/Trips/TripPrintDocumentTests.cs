using Bunit;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

// User Story 2 (P2): the printable document shows every detail — metadata, legs as
// chronological row dividers, event columns, and combined datetime+TZ cells.
public class TripPrintDocumentTests : TestContext
{
    private IRenderedComponent<TripPrintDocument> Render(TripDetail trip) =>
        RenderComponent<TripPrintDocument>(p => p.Add(x => x.Trip, TripPrintFormatting.BuildPrintableTrip(trip)));

    [Fact]
    public void RendersMetadataBlockFirst()
    {
        var cut = Render(TripFixtures.Populated());

        Assert.Contains("Japan 2026", cut.Find("h1.tp-print-title").TextContent);
        Assert.Contains("07/14/2026", cut.Find(".tp-print-dates").TextContent);
        Assert.Contains("Cherry blossoms", cut.Markup);
    }

    [Fact]
    public void RendersLegsAsChronologicalRowDividers()
    {
        var cut = Render(TripFixtures.Populated());

        var dividers = cut.FindAll("tr.tp-print-leg .tp-print-leg-title");
        Assert.Equal(new[] { "Arrival", "Departure" }, dividers.Select(d => d.TextContent.Trim()).ToArray());
        // Divider spans all event columns.
        Assert.Equal("7", cut.Find("tr.tp-print-leg th").GetAttribute("colspan"));
    }

    [Fact]
    public void RendersEventRowsWithCombinedDateTimeAndZone()
    {
        var cut = Render(TripFixtures.Populated());

        var rows = cut.FindAll("tr.tp-print-event");
        Assert.NotEmpty(rows);
        Assert.Contains("07/14/2026 09:30 EDT", cut.Markup);
    }

    [Fact]
    public void MissingOptionalFields_RenderEmptyCellsNotErrors()
    {
        var cut = Render(TripFixtures.Populated());

        // The "Walk" event has no location, end, confirmation, cost, or notes.
        var walkRow = cut.FindAll("tr.tp-print-event")
            .First(r => r.QuerySelectorAll("td")[1].TextContent.Trim() == "Walk");
        var cells = walkRow.QuerySelectorAll("td");
        Assert.Equal(string.Empty, cells[2].TextContent.Trim()); // Location
        Assert.Equal(string.Empty, cells[4].TextContent.Trim()); // End
        Assert.Equal(string.Empty, cells[6].TextContent.Trim()); // Est. Cost
    }

    [Fact]
    public void EmptyTrip_ShowsEmptyStateNotTable()
    {
        var cut = Render(TripFixtures.Empty());

        Assert.Empty(cut.FindAll("table.tp-print-table"));
        Assert.Contains("no legs or events", cut.Markup);
    }
}
