using TripPlanner.Api.Features.Timezones;
using TripPlanner.Api.Features.TripItems;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.TripItems;

public class TrackedItemEndpointTests
{
    private static TrackedItemValidator NewValidator() => new(new TimezoneIdValidator());

    private static readonly DateTime Start = new(2026, 7, 11, 9, 0, 0);

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
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "flight", "Hop", null,
            Start, "UTC", null, null, "slate", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("itemType", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsMissingLeg_WhenTripHasNoLegs()
    {
        var trip = new TripDetail(Guid.NewGuid(), "Trip", null,
            new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>());
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(Guid.Empty, "activity", "Tour", null,
            Start, "UTC", null, null, "slate", null, null), trip);
        Assert.False(r.IsValid);
        Assert.Equal("tripLegId", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsLegFromDifferentTrip()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(Guid.NewGuid(), "activity", "Tour", null,
            Start, "UTC", null, null, "slate", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("tripLegId", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsInvalidColor()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            Start, "UTC", null, null, "chartreuse", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("displayColor", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsInvalidStartTimezone()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            Start, "Not/AZone", null, null, "teal", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("startTimeZoneId", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsMissingEndTimezone_WhenEndProvided()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            Start, "UTC", Start.AddHours(2), null, "teal", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("endTimeZoneId", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsEndBeforeStart()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            Start, "UTC", Start.AddHours(-2), "UTC", "teal", null, null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("endLocal", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_RejectsConfirmationCodeOver255()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            Start, "UTC", null, null, "teal", new string('x', 256), null), TripWithLeg(tripId, legId));
        Assert.False(r.IsValid);
        Assert.Equal("confirmationCode", r.Error!.Details!["field"]);
    }

    [Fact]
    public void Validator_AcceptsValidLegAndColor()
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var v = NewValidator();
        var r = v.Validate(new CreateTrackedItemRequest(legId, "activity", "Tour", null,
            Start, "UTC", Start.AddHours(2), "UTC", "teal", "ABC123", "Window seat"), TripWithLeg(tripId, legId));
        Assert.True(r.IsValid);
    }
}
