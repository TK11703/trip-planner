using TripPlanner.Api.Features.Timezones;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.TripItems;

public sealed class TrackedItemValidator
{
    private const int ConfirmationCodeMaxLength = 255;
    private const int NotesMaxLength = 2000;
    private const decimal EstimatedCostMax = 9_999_999_999.99m;

    private readonly ITimezoneIdValidator _timezones;

    public TrackedItemValidator(ITimezoneIdValidator timezones)
    {
        _timezones = timezones;
    }

    public ValidationResult Validate(CreateTrackedItemRequest request, TripDetail trip)
        => ValidateCore(request.TripLegId, request.ItemType, request.Title, request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId, request.DisplayColor, request.ConfirmationCode, request.Notes, request.EstimatedCost, trip);

    public ValidationResult Validate(UpdateTrackedItemRequest request, TripDetail trip)
        => ValidateCore(request.TripLegId, request.ItemType, request.Title, request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId, request.DisplayColor, request.ConfirmationCode, request.Notes, request.EstimatedCost, trip);

    private ValidationResult ValidateCore(Guid? tripLegId, string itemType, string title, DateTime startLocal, string startTimeZoneId, DateTime? endLocal, string? endTimeZoneId, string displayColor, string? confirmationCode, string? notes, decimal? estimatedCost, TripDetail trip)
    {
        if (string.IsNullOrWhiteSpace(itemType) || !TrackedItemTypes.All.Contains(itemType))
            return ValidationResult.Fail("Item type must be one of: event, reservation, activity, reminder.", "itemType");
        var v = ValidationProblemDetailsFactory.RequireNonEmpty(title, "title", "Title is required.");
        if (!v.IsValid) return v;

        var startTimeZone = _timezones.FindTimeZone(startTimeZoneId ?? string.Empty);
        if (startTimeZone is null)
            return ValidationResult.Fail("Select a valid start timezone.", "startTimeZoneId");

        TimeZoneInfo? endTimeZone = null;
        if (endLocal is not null)
        {
            if (string.IsNullOrWhiteSpace(endTimeZoneId))
                return ValidationResult.Fail("Select an end timezone for the item end.", "endTimeZoneId");
            endTimeZone = _timezones.FindTimeZone(endTimeZoneId);
            if (endTimeZone is null)
                return ValidationResult.Fail("Select a valid end timezone.", "endTimeZoneId");
        }

        if (endLocal is { } end && endTimeZone is not null)
        {
            var startInstant = TripInstant.ToInstant(startLocal, startTimeZone);
            var endInstant = TripInstant.ToInstant(end, endTimeZone);
            if (endInstant < startInstant)
                return ValidationResult.Fail("End must be on or after start.", "endLocal");
        }

        if (!TrackedItemColors.IsValid(displayColor))
            return ValidationResult.Fail("Select a valid item color.", "displayColor");
        if (confirmationCode is not null && confirmationCode.Length > ConfirmationCodeMaxLength)
            return ValidationResult.Fail($"Confirmation/Reservation Code must be {ConfirmationCodeMaxLength} characters or fewer.", "confirmationCode");
        if (notes is not null && notes.Length > NotesMaxLength)
            return ValidationResult.Fail($"Notes must be {NotesMaxLength} characters or fewer.", "notes");
        if (estimatedCost is { } cost)
        {
            if (cost < 0)
                return ValidationResult.Fail("Estimated cost cannot be negative.", "estimatedCost");
            if (cost > EstimatedCostMax)
                return ValidationResult.Fail("Estimated cost is too large.", "estimatedCost");
            if (decimal.Round(cost, 2) != cost)
                return ValidationResult.Fail("Enter an estimated cost with up to two decimal places.", "estimatedCost");
        }
        // A leg is optional: an item the traveler cannot place yet lands in the timeline's
        // unassigned area. But once a leg is chosen, the leg's travel window is binding.
        if (tripLegId is not { } legId || legId == Guid.Empty)
            return ValidationResult.Success;

        var leg = trip.Legs.FirstOrDefault(l => l.TripLegId == legId);
        if (leg is null)
            return ValidationResult.Fail("The selected trip leg does not belong to this trip.", "tripLegId");

        // An item has to happen while the traveler is on that leg, so both ends of the item are
        // compared as instants against the leg's own travel window.
        var legStartZone = _timezones.FindTimeZone(leg.StartTimeZoneId ?? string.Empty);
        var legEndZone = _timezones.FindTimeZone(leg.EndTimeZoneId ?? string.Empty);
        if (legStartZone is not null && legEndZone is not null)
        {
            var legStart = TripInstant.ToInstant(leg.StartLocal, legStartZone);
            var legEnd = TripInstant.ToInstant(leg.EndLocal, legEndZone);

            var start = TripInstant.ToInstant(startLocal, startTimeZone);
            if (start < legStart || start > legEnd)
                return ValidationResult.Fail("Start must fall within the selected trip leg's travel dates.", "startLocal");

            if (endLocal is { } itemEnd && endTimeZone is not null)
            {
                var itemEndInstant = TripInstant.ToInstant(itemEnd, endTimeZone);
                if (itemEndInstant < legStart || itemEndInstant > legEnd)
                    return ValidationResult.Fail("End must fall within the selected trip leg's travel dates.", "endLocal");
            }
        }
        return ValidationResult.Success;
    }
}
