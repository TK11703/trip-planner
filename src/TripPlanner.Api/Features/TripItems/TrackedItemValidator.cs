using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.TripItems;

public sealed class TrackedItemValidator
{
    public ValidationResult Validate(CreateTrackedItemRequest request, TripDetail trip)
        => ValidateCore(request.ItemType, request.Title, request.StartsAt, request.EndsAt, trip);

    public ValidationResult Validate(UpdateTrackedItemRequest request, TripDetail trip)
        => ValidateCore(request.ItemType, request.Title, request.StartsAt, request.EndsAt, trip);

    private static ValidationResult ValidateCore(string itemType, string title, DateTimeOffset startsAt, DateTimeOffset? endsAt, TripDetail trip)
    {
        if (string.IsNullOrWhiteSpace(itemType) || !TrackedItemTypes.All.Contains(itemType))
            return ValidationResult.Fail("Item type must be one of: event, reservation, activity, reminder.", "itemType");
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(title, "title", "Title is required.");
        if (!v.IsValid) return v;
        v = ValidationProblemDetailsFactory.RequireEndOnOrAfterStart(startsAt, endsAt, "endsAt", "End must be on or after start.");
        if (!v.IsValid) return v;
        var d = DateOnly.FromDateTime(startsAt.UtcDateTime);
        if (d < trip.StartDate || d > trip.EndDate)
            return ValidationResult.Fail("Item date must fall within the trip date range. Update the trip date range first if needed.", "startsAt");
        return ValidationResult.Success;
    }
}
