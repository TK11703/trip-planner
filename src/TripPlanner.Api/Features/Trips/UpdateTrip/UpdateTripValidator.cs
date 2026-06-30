using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.Trips.UpdateTrip;

public sealed class UpdateTripValidator
{
    public ValidationResult Validate(UpdateTripRequest request)
    {
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(request.Name, "name", "Trip name is required.");
        if (!v.IsValid) return v;
        return ValidationProblemDetailsFactory.RequireEndOnOrAfterStart(
            request.StartDate, request.EndDate, "endDate",
            "The trip end date must be on or after the start date.");
    }
}
