using TripPlanner.Api.Features.Timezones;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.TripItems;

public sealed class TripLegValidator
{
    private readonly ITimezoneIdValidator _timezones;

    public TripLegValidator(ITimezoneIdValidator timezones)
    {
        _timezones = timezones;
    }

    public ValidationResult Validate(CreateTripLegRequest request, TripDetail trip)
        => ValidateCore(request.Title, request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId, trip);

    public ValidationResult Validate(UpdateTripLegRequest request, TripDetail trip)
        => ValidateCore(request.Title, request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId, trip);

    private ValidationResult ValidateCore(string title, DateTime startLocal, string startTimeZoneId, DateTime endLocal, string endTimeZoneId, TripDetail trip)
    {
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(title, "title", "Leg title is required.");
        if (!v.IsValid) return v;

        var startTimeZone = _timezones.FindTimeZone(startTimeZoneId);
        if (startTimeZone is null)
        {
            return ValidationResult.Fail("Select a valid start timezone.", "startTimeZoneId");
        }

        var endTimeZone = _timezones.FindTimeZone(endTimeZoneId);
        if (endTimeZone is null)
        {
            return ValidationResult.Fail("Select a valid end timezone.", "endTimeZoneId");
        }

        var startInstant = ToInstant(startLocal, startTimeZone);
        var endInstant = ToInstant(endLocal, endTimeZone);
        if (endInstant < startInstant)
        {
            return ValidationResult.Fail("End must be on or after start.", "endLocal");
        }

        var startDate = DateOnly.FromDateTime(startLocal);
        var endDate = DateOnly.FromDateTime(endLocal);
        if (startDate < trip.StartDate || startDate > trip.EndDate || endDate < trip.StartDate || endDate > trip.EndDate)
        {
            return ValidationResult.Fail("Leg start and end dates must fall within the trip date range. Update the trip date range first if needed.", "startLocal");
        }

        return ValidationResult.Success;
    }

    private static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(unspecifiedLocal);
        return new DateTimeOffset(unspecifiedLocal, offset).ToUniversalTime();
    }
}
