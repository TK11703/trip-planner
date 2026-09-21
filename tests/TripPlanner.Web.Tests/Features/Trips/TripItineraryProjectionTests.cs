using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Features.Trips;

public class TripItineraryProjectionTests
{
    [Fact]
    public void BuildItineraryTable_OrdersLegsAndTheirItemsChronologically()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());

        Assert.Equal(
            new[] { "Arrival", "Free day", "Departure" },
            model.Legs.Select(leg => leg.Title).ToArray());
        Assert.Equal(
            new[] { "Walk", "Dinner" },
            model.Legs[0].Items.Select(item => item.Title).ToArray());
        Assert.Empty(model.Legs[1].Items);
    }

    [Fact]
    public void BuildItineraryTable_GroupsNullAndMissingLegItemsAsUnassigned()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());

        Assert.Equal(
            new[] { "Pack", "Review transfer" },
            model.UnassignedItems.Select(item => item.Title).ToArray());
    }

    [Fact]
    public void BuildItineraryTable_RetainsStableLegAndItemIdentifiers()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());

        Assert.Equal(TripFixtures.ArrivalLegId, model.Legs[0].TripLegId);
        Assert.Equal(TripFixtures.WalkItemId, model.Legs[0].Items[0].TrackedItemId);
        Assert.Equal(TripFixtures.ArrivalLegId, model.Legs[0].Items[0].TripLegId);
        Assert.Equal(TripFixtures.UnassignedItemId, model.UnassignedItems[0].TrackedItemId);
        Assert.Null(model.UnassignedItems[0].TripLegId);
        Assert.Equal(TripFixtures.MissingLegItemId, model.UnassignedItems[1].TrackedItemId);
        Assert.NotNull(model.UnassignedItems[1].TripLegId);
    }

    [Fact]
    public void BuildItineraryTable_CarriesLegEligibilityAndFormattedValues()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());

        var arrival = model.Legs[0];
        Assert.True(arrival.CanContainItems);
        Assert.Equal("Car", arrival.ModeText);
        Assert.Equal("Seattle \u2192 Tokyo", arrival.RouteText);
        Assert.Equal("07/14/2026 08:00 EDT", arrival.StartText);

        var departure = model.Legs[2];
        Assert.False(departure.CanContainItems);
        Assert.Equal("Flight", departure.ModeText);

        var dinner = arrival.Items[1];
        Assert.Equal("Event", dinner.TypeText);
        Assert.Equal("07/14/2026 19:00 EDT", dinner.StartText);
        Assert.Equal("07/14/2026 21:00 EDT", dinner.EndText);
        Assert.Equal("$80.00", dinner.EstimatedCostText);
    }

    [Fact]
    public void BuildItineraryTable_PreservesMissingOptionalValues()
    {
        var model = TripPrintFormatting.BuildItineraryTable(TripFixtures.Representative());

        var walk = model.Legs[0].Items[0];
        Assert.Null(walk.Location);
        Assert.Null(walk.EndText);
        Assert.Null(walk.ConfirmationCode);
        Assert.Null(walk.EstimatedCostText);
        Assert.True(model.HasContent);

        Assert.False(TripPrintFormatting.BuildItineraryTable(TripFixtures.Empty()).HasContent);
    }
}