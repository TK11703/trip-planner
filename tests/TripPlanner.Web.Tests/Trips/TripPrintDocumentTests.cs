using Bunit;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

// User Story 2 (P2): the printable document shows every detail — metadata, legs as
// chronological row dividers, item columns, and combined datetime+TZ cells.
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

        var dividers = cut.FindAll("tr.tp-itinerary-leg .tp-itinerary-leg-title");
        Assert.Equal(new[] { "Arrival", "Departure" }, dividers.Select(d => d.TextContent.Trim()).ToArray());
        // Divider spans all item columns.
        Assert.Equal("7", cut.Find("tr.tp-itinerary-leg th").GetAttribute("colspan"));
    }

    [Fact]
    public void RendersItemRowsWithCombinedDateTimeAndZone()
    {
        var cut = Render(TripFixtures.Populated());

        var rows = cut.FindAll("tr.tp-itinerary-item");
        Assert.NotEmpty(rows);
        Assert.Contains("07/14/2026 09:30 EDT", cut.Markup);
    }

    [Fact]
    public void PrintableModelMatchesNeutralTableProjection()
    {
        var trip = TripFixtures.Representative();
        var table = TripPrintFormatting.BuildItineraryTable(trip);
        var printable = TripPrintFormatting.BuildPrintableTrip(trip);

        Assert.Equal(table.Legs.Select(leg => leg.Title), printable.Legs.Select(leg => leg.Title));
        Assert.Equal(
            table.Legs.SelectMany(leg => leg.Items).Select(item => item.Title),
            printable.Legs.SelectMany(leg => leg.Items).Select(item => item.Title));
        Assert.Equal(
            table.UnassignedItems.Select(item => item.Title),
            printable.UnassignedItems.Select(item => item.Title));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DelegatesPopulatedAndEmptyRowsToSharedTable(bool populated)
    {
        var trip = populated ? TripFixtures.Representative() : TripFixtures.Empty();
        var model = TripPrintFormatting.BuildItineraryTable(trip);
        var expected = RenderComponent<TripItineraryTable>(parameters => parameters
            .Add(component => component.Model, model));

        var document = Render(trip);
        var sharedTable = document.FindComponent<TripItineraryTable>();

        Assert.Equal(expected.Markup, sharedTable.Markup);
    }

    [Fact]
    public void SharedPrintTableOmitsInteractiveControls()
    {
        var cut = Render(TripFixtures.Representative());

        var table = cut.FindComponent<TripItineraryTable>();
        Assert.Empty(table.FindAll("button, input, select, textarea"));
    }

    [Fact]
    public void MissingOptionalFields_RenderEmptyCellsNotErrors()
    {
        var cut = Render(TripFixtures.Populated());

        // The "Walk" item has no location, end, confirmation, cost, or notes.
        var walkRow = cut.FindAll("tr.tp-itinerary-item")
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

        Assert.Empty(cut.FindAll("table.tp-itinerary-table"));
        Assert.Contains("no legs or items", cut.Markup);
    }

    // --- Feature 025: the paper copy names the mode and the booking ---

    [Theory]
    [InlineData(TransportationModes.Flight, "Flight")]
    [InlineData(TransportationModes.Train, "Train")]
    [InlineData(TransportationModes.Bus, "Bus")]
    [InlineData(TransportationModes.Boat, "Boat")]
    [InlineData(TransportationModes.Car, "Car")]
    public void TravelLeg_PrintsItsTransportationMode(string mode, string label)
    {
        var cut = Render(TripFixtures.WithTravelLeg(mode));

        Assert.Equal(label, cut.Find(".tp-itinerary-leg-mode").TextContent.Trim());
        Assert.Contains("Seattle", cut.Find(".tp-itinerary-leg-route").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void StayLeg_PrintsNoTransportationMode()
    {
        var cut = Render(TripFixtures.WithStayLeg());

        Assert.Empty(cut.FindAll(".tp-itinerary-leg-mode"));
    }

    [Fact]
    public void TravelLeg_PrintsBookingDetails()
    {
        var cut = Render(TripFixtures.WithTravelLeg(TransportationModes.Flight, travelCost: 412.50m, confirmationCode: "ABC123"));

        Assert.Contains("ABC123", cut.Find(".tp-itinerary-leg-confirmation").TextContent, StringComparison.Ordinal);
        Assert.Contains("412", cut.Find(".tp-itinerary-leg-cost").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TravelLeg_WithoutBookingDetails_PrintsNeither()
    {
        var cut = Render(TripFixtures.WithTravelLeg(TransportationModes.Boat));

        Assert.Empty(cut.FindAll(".tp-itinerary-leg-confirmation"));
        Assert.Empty(cut.FindAll(".tp-itinerary-leg-cost"));
    }
}
