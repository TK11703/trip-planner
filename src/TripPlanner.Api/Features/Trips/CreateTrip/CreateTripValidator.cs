using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.Trips.CreateTrip;

public sealed class CreateTripValidator
{
    public ValidationResult Validate(CreateTripRequest request)
    {
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(request.Name, "name", "Trip name is required.");
        if (!v.IsValid) return v;
        return ValidationProblemDetailsFactory.RequireEndOnOrAfterStart(
            request.StartDate, request.EndDate, "endDate",
            "The trip end date must be on or after the start date.");
    }
}
