using TripPlanner.Api.Features.Timezones;
using TripPlanner.Api.Features.TripItems;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Api.Tests.FavoriteDestinations;

public sealed class FavoriteTrackedItemEligibilityTests
{
    private static readonly DateTime Start = new(2026, 9, 5, 9, 0, 0);

    [Fact]
    public void CopiedFavoriteValues_UseExistingTrackedItemValidatorAndEligibleLegWindow()
    {
        var trip = TripWithStayLeg(out var eligibleLegId, out _);
        var favorite = Favorite();
        var request = CreateFromFavorite(favorite, eligibleLegId, Start);

        var result = NewValidator().Validate(request, trip);

        Assert.True(result.IsValid);
        Assert.Equal("Museum Island", request.Title);
        Assert.Equal("Bodestraße 1, Berlin", request.Location);
        Assert.Equal("Allow an afternoon", request.Notes);
    }

    [Fact]
    public void FavoriteValues_DoNotRequireAnAssignedLegWhenExistingRulesAllowUnassignedItems()
    {
        var trip = TripWithStayLeg(out _, out _);
        var request = CreateFromFavorite(Favorite(), null, Start);

        var result = NewValidator().Validate(request, trip);

        Assert.True(result.IsValid);
        Assert.Null(request.TripLegId);
    }

    [Fact]
    public void FavoriteSelectionDoesNotBypassRequiredScheduleOrLegWindowValidation()
    {
        var trip = TripWithStayLeg(out var eligibleLegId, out _);
        var validator = NewValidator();
        var invalidSchedule = validator.Validate(CreateFromFavorite(Favorite(), eligibleLegId, new DateTime(2026, 9, 20, 9, 0, 0)), trip);
        var restrictedLegTrip = TripWithStayLeg(out _, out var restrictedLegId);
        var invalidLeg = validator.Validate(CreateFromFavorite(Favorite(), restrictedLegId, Start), restrictedLegTrip);
        var missingTimezone = validator.Validate(CreateFromFavorite(Favorite(), eligibleLegId, Start) with { StartTimeZoneId = "invalid" }, trip);

        Assert.False(invalidSchedule.IsValid);
        Assert.Equal("startLocal", invalidSchedule.Error!.Details!["field"]);
        Assert.False(invalidLeg.IsValid);
        Assert.Equal("tripLegId", invalidLeg.Error!.Details!["field"]);
        Assert.False(missingTimezone.IsValid);
        Assert.Equal("startTimeZoneId", missingTimezone.Error!.Details!["field"]);
    }

    private static FavoriteDestinationDto Favorite()
        => new(Guid.NewGuid(), "Museum Island", "Bodestraße 1, Berlin", "Berlin", "Germany", 52.5169, 13.4019, "Allow an afternoon", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static CreateTrackedItemRequest CreateFromFavorite(FavoriteDestinationDto favorite, Guid? legId, DateTime start)
        => new(legId, "activity", favorite.Name, favorite.Address, start, "UTC", null, null, "blue", null, favorite.Notes);

    private static TrackedItemValidator NewValidator() => new(new TimezoneIdValidator());

    private static TripDetail TripWithStayLeg(out Guid eligibleLegId, out Guid restrictedLegId)
    {
        var tripId = Guid.NewGuid();
        eligibleLegId = Guid.NewGuid();
        restrictedLegId = Guid.NewGuid();
        var start = new DateTime(2026, 9, 1, 8, 0, 0);
        var end = new DateTime(2026, 9, 10, 18, 0, 0);
        var legs = new[]
        {
            Leg(tripId, eligibleLegId, "stay", "",
                start, end),
            Leg(tripId, restrictedLegId, "travel", "flight", start, end)
        };
        return new TripDetail(tripId, "Trip", null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, legs, Array.Empty<TrackedItemDto>());
    }

    private static TripLegDto Leg(Guid tripId, Guid legId, string kind, string mode, DateTime start, DateTime end)
        => new(legId, tripId, "Leg", "Origin", "Destination", start, "UTC", "UTC", end, "UTC", "UTC", null, 0, kind, mode);
}