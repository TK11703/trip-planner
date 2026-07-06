using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.TripItems;

public sealed class TrackedItemValidator
{
    public ValidationResult Validate(CreateTrackedItemRequest request, TripDetail trip)
        => ValidateCore(request.TripLegId, request.ItemType, request.Title, request.StartsAt, request.EndsAt, request.DisplayColor, trip);

    public ValidationResult Validate(UpdateTrackedItemRequest request, TripDetail trip)
        => ValidateCore(request.TripLegId, request.ItemType, request.Title, request.StartsAt, request.EndsAt, request.DisplayColor, trip);

    private static ValidationResult ValidateCore(Guid tripLegId, string itemType, string title, DateTimeOffset startsAt, DateTimeOffset? endsAt, string displayColor, TripDetail trip)
    {
        if (string.IsNullOrWhiteSpace(itemType) || !TrackedItemTypes.All.Contains(itemType))
            return ValidationResult.Fail("Item type must be one of: event, reservation, activity, reminder.", "itemType");
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(title, "title", "Title is required.");
        if (!v.IsValid) return v;
        v = ValidationProblemDetailsFactory.RequireEndOnOrAfterStart(startsAt, endsAt, "endsAt", "End must be on or after start.");
        if (!v.IsValid) return v;
        if (!TrackedItemColors.IsValid(displayColor))
            return ValidationResult.Fail("Select a valid event color.", "displayColor");
        if (trip.Legs.Count == 0)
            return ValidationResult.Fail("Add a trip leg before adding an event, then relate the event to that leg.", "tripLegId");
        if (tripLegId == Guid.Empty)
            return ValidationResult.Fail("Select the trip leg this event belongs to.", "tripLegId");
        if (trip.Legs.All(l => l.TripLegId != tripLegId))
            return ValidationResult.Fail("The selected trip leg does not belong to this trip.", "tripLegId");
        return ValidationResult.Success;
    }
}
