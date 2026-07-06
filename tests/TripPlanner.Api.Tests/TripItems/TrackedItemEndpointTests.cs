using TripPlanner.Api.Features.TripItems;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.TripItems;

public class TrackedItemEndpointTests
{
    private static TripLegDto Leg(Guid tripId, Guid legId) => new(
        legId, tripId, "Paris", "Chicago", "Paris",
        new DateTime(2026, 7, 11, 8, 0, 0), "UTC", "UTC",
        new DateTime(2026, 7, 12, 8, 0, 0), "UTC", "UTC",
        null, 0);

    private static TripDetail TripWithLeg(Guid tripId, Guid legId) => new(
        tripId, "Trip", null,
        new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        new[] { Leg(tripId, legId) }, Array.Empty<TrackedItemDto>());

    [Fact]
    public void Validator_RejectsUnsupportedItemType()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = new TrackedItemValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "flight", "Hop", null,
            new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero), null, "slate", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("itemType", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsMissingLeg_WhenTripHasNoLegs()
    {
        var trip = new TripDetail(Guid.NewGuid(), "Trip", null,
            new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>());
        var v = new TrackedItemValidator();
        var r = v.Validate(new CreateTrackedItemRequest(Guid.Empty, "activity", "Tour", null,
            new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero), null, "slate", null, null), trip);
        Assert.False(r.IsValid);
        Assert.Equal("tripLegId", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsLegFromDifferentTrip()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = new TrackedItemValidator();
        var r = v.Validate(new CreateTrackedItemRequest(Guid.NewGuid(), "activity", "Tour", null,
            new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero), null, "slate", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("tripLegId", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsInvalidColor()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = new TrackedItemValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero), null, "chartreuse", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("displayColor", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_AcceptsValidLegAndColor()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = new TrackedItemValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            new DateTimeOffset(2026, 7, 11, 9, 0, 0, TimeSpan.Zero), null, "teal", null, null), TripWithLeg(tripId, legId));
        Assert.True(r.IsValid);
    }
}
