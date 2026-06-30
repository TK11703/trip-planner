using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.TripItems;

public sealed class TripLegValidator
{
    public ValidationResult Validate(CreateTripLegRequest request, TripDetail trip)
        => ValidateCore(request.Title, request.StartAt, request.EndAt, trip);

    public ValidationResult Validate(UpdateTripLegRequest request, TripDetail trip)
        => ValidateCore(request.Title, request.StartAt, request.EndAt, trip);

    private static ValidationResult ValidateCore(string title, DateTimeOffset startAt, DateTimeOffset? endAt, TripDetail trip)
    {
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(title, "title", "Leg title is required.");
        if (!v.IsValid) return v;
        v = ValidationProblemDetailsFactory.RequireEndOnOrAfterStart(startAt, endAt, "endAt", "End must be on or after start.");
        if (!v.IsValid) return v;
        var d = DateOnly.FromDateTime(startAt.UtcDateTime);
        if (d < trip.StartDate || d > trip.EndDate)
            return ValidationResult.Fail("Leg date must fall within the trip date range. Update the trip date range first if needed.", "startAt");
        return ValidationResult.Success;
    }
}
