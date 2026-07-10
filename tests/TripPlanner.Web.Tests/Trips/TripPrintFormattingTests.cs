using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

// Foundational: pure formatting/ordering/grouping helpers behind the printable trip page.
public class TripPrintFormattingTests
{
    [Fact]
    public void FormatDateTimeWithZone_CombinesDateTimeAndShortZone()
    {
        var result = TripPrintFormatting.FormatDateTimeWithZone(new DateTime(2026, 7, 14, 9, 30, 0), "America/New_York");

        // July is daylight saving in New York -> EDT.
        Assert.Equal("07/14/2026 09:30 EDT", result);
    }

    [Fact]
    public void FormatDateTimeWithZone_Uses24HourTime()
    {
        var result = TripPrintFormatting.FormatDateTimeWithZone(new DateTime(2026, 7, 14, 18, 5, 0), "America/Los_Angeles");

        // July is daylight saving on the US west coast -> PDT.
        Assert.Equal("07/14/2026 18:05 PDT", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatDateTimeWithZone_BlankZone_OmitsZoneToken(string? zone)
    {
        var result = TripPrintFormatting.FormatDateTimeWithZone(new DateTime(2026, 12, 1, 0, 0, 0), zone);

        Assert.Equal("12/01/2026 00:00", result);
    }

    [Fact]
    public void GetShortZoneName_Utc_ReturnsUtc()
    {
        Assert.Equal("UTC", TripPrintFormatting.GetShortZoneName("UTC", new DateTime(2026, 7, 14)));
    }

    [Fact]
    public void GetShortZoneName_UnknownZone_FallsBackToId()
    {
        Assert.Equal("Not/AZone", TripPrintFormatting.GetShortZoneName("Not/AZone", new DateTime(2026, 7, 14)));
    }

    [Fact]
    public void OrderLegsChronologically_SortsByStart()
    {
        var later = Leg("Second", new DateTime(2026, 7, 15, 8, 0, 0), sortOrder: 0);
        var earlier = Leg("First", new DateTime(2026, 7, 14, 8, 0, 0), sortOrder: 1);

        var ordered = TripPrintFormatting.OrderLegsChronologically(new[] { later, earlier });

        Assert.Equal("First", ordered[0].Title);
        Assert.Equal("Second", ordered[1].Title);
    }

    [Fact]
    public void GroupEventsByLeg_PartitionsAndOrders()
    {
        var leg = Leg("Leg", new DateTime(2026, 7, 14, 8, 0, 0));
        var second = Item(leg.TripLegId, "B", new DateTime(2026, 7, 14, 12, 0, 0), sortOrder: 0);
        var first = Item(leg.TripLegId, "A", new DateTime(2026, 7, 14, 9, 0, 0), sortOrder: 1);
        var orphan = Item(null, "Orphan", new DateTime(2026, 7, 14, 10, 0, 0));

        var (byLeg, unassigned) = TripPrintFormatting.GroupEventsByLeg(new[] { leg }, new[] { second, first, orphan });

        Assert.Equal(new[] { "A", "B" }, byLeg[leg.TripLegId].Select(i => i.Title).ToArray());
        Assert.Single(unassigned);
        Assert.Equal("Orphan", unassigned[0].Title);
    }

    [Fact]
    public void BuildPrintableTrip_MapsMetadataLegsAndMissingOptionals()
    {
        var leg = Leg("Flight", new DateTime(2026, 7, 14, 8, 0, 0), origin: "Seattle", destination: "Tokyo");
        var withCost = Item(leg.TripLegId, "Dinner", new DateTime(2026, 7, 14, 19, 0, 0), location: "Ginza", cost: 80m);
        var noOptionals = Item(leg.TripLegId, "Walk", new DateTime(2026, 7, 14, 9, 0, 0));
        var trip = Trip("Japan 2026", "Cherry blossoms", new[] { leg }, new[] { withCost, noOptionals });

        var printable = TripPrintFormatting.BuildPrintableTrip(trip);

        Assert.Equal("Japan 2026", printable.Name);
        Assert.Equal("Cherry blossoms", printable.Description);
        Assert.True(printable.HasContent);
        var printedLeg = Assert.Single(printable.Legs);
        Assert.Equal("Seattle \u2192 Tokyo", printedLeg.RouteText);
        Assert.Equal(new[] { "Walk", "Dinner" }, printedLeg.Events.Select(e => e.Title).ToArray());
        var walk = printedLeg.Events[0];
        Assert.Null(walk.Location);
        Assert.Null(walk.EstimatedCostText);
        Assert.Null(walk.EndText);
    }

    [Fact]
    public void BuildPrintableTrip_EmptyTrip_HasNoContent()
    {
        var trip = Trip("Empty", null, Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>());

        var printable = TripPrintFormatting.BuildPrintableTrip(trip);

        Assert.False(printable.HasContent);
        Assert.Null(printable.Description);
    }

    private static TripLegDto Leg(string title, DateTime start, int sortOrder = 0, string? origin = null, string? destination = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), title, origin, destination, start, "America/New_York", "America/New_York",
            start.AddHours(3), "America/New_York", "America/New_York", null, sortOrder);

    private static TrackedItemDto Item(Guid? legId, string title, DateTime start, int sortOrder = 0, string? location = null, decimal? cost = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), legId, TrackedItemTypes.Event, title, location, start, "America/New_York",
            new DateTimeOffset(start, TimeSpan.FromHours(-4)), null, null, null, TrackedItemColors.Default, null, null, sortOrder, cost);

    private static TripDetail Trip(string name, string? description, IReadOnlyList<TripLegDto> legs, IReadOnlyList<TrackedItemDto> items) =>
        new(Guid.NewGuid(), name, description, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, legs, items);
}
