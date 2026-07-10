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

    private ValidationResult ValidateCore(Guid tripLegId, string itemType, string title, DateTime startLocal, string startTimeZoneId, DateTime? endLocal, string? endTimeZoneId, string displayColor, string? confirmationCode, string? notes, decimal? estimatedCost, TripDetail trip)
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
                return ValidationResult.Fail("Select an end timezone for the event end.", "endTimeZoneId");
            endTimeZone = _timezones.FindTimeZone(endTimeZoneId);
            if (endTimeZone is null)
                return ValidationResult.Fail("Select a valid end timezone.", "endTimeZoneId");
        }

        if (endLocal is { } end && endTimeZone is not null)
        {
            var startInstant = ToInstant(startLocal, startTimeZone);
            var endInstant = ToInstant(end, endTimeZone);
            if (endInstant < startInstant)
                return ValidationResult.Fail("End must be on or after start.", "endLocal");
        }

        if (!TrackedItemColors.IsValid(displayColor))
            return ValidationResult.Fail("Select a valid event color.", "displayColor");
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
        if (trip.Legs.Count == 0)
            return ValidationResult.Fail("Add a trip leg before adding an event, then relate the event to that leg.", "tripLegId");
        if (tripLegId == Guid.Empty)
            return ValidationResult.Fail("Select the trip leg this event belongs to.", "tripLegId");
        if (trip.Legs.All(l => l.TripLegId != tripLegId))
            return ValidationResult.Fail("The selected trip leg does not belong to this trip.", "tripLegId");
        return ValidationResult.Success;
    }

    private static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(unspecifiedLocal);
        return new DateTimeOffset(unspecifiedLocal, offset).ToUniversalTime();
    }
}
