using TripPlanner.Api.Features.Timezones;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Validation;

namespace TripPlanner.Api.Features.TripItems;

public sealed class TripLegValidator
{
    private const int ConfirmationCodeMaxLength = 255;
    private const decimal TravelCostMax = 9_999_999_999.99m;

    private readonly ITimezoneIdValidator _timezones;

    public TripLegValidator(ITimezoneIdValidator timezones)
    {
        _timezones = timezones;
    }

    public ValidationResult Validate(CreateTripLegRequest request, TripDetail trip)
        => ValidateCore(request.Title, request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId, trip,
            request.LegKind, request.TransportationMode, request.Origin, request.Destination, request.TravelCost, request.ConfirmationCode);

    public ValidationResult Validate(UpdateTripLegRequest request, TripDetail trip)
        => ValidateCore(request.Title, request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId, trip,
            request.LegKind, request.TransportationMode, request.Origin, request.Destination, request.TravelCost, request.ConfirmationCode);

    private ValidationResult ValidateCore(string title, DateTime startLocal, string startTimeZoneId, DateTime endLocal, string endTimeZoneId, TripDetail trip,
        string? legKind, string? transportationMode, string? origin, string? destination, decimal? travelCost, string? confirmationCode)
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

        return ValidateShape(legKind, transportationMode, origin, destination, travelCost, confirmationCode);
    }

    /// <summary>
    /// Checks what the leg claims to be. A Stay's travel-only values are normalized away rather
    /// than rejected, so only genuinely unusable input is reported back to the traveler.
    /// </summary>
    private static ValidationResult ValidateShape(string? legKind, string? transportationMode, string? origin, string? destination, decimal? travelCost, string? confirmationCode)
    {
        if (!string.IsNullOrWhiteSpace(legKind) && !TripLegKinds.IsValid(legKind))
            return ValidationResult.Fail("Choose whether this leg is a stay or travel.", "legKind");

        var shape = TripLegShape.Resolve(legKind, transportationMode, origin, destination, travelCost, confirmationCode);
        if (!shape.IsTravel)
            return ValidationResult.Success;

        if (shape.TransportationMode is null)
            return ValidationResult.Fail("Choose how you are traveling: flight, train, bus, boat, or car.", "transportationMode");

        if (shape.Origin is null)
            return ValidationResult.Fail("Enter where this travel leg starts from.", "origin");

        if (shape.Destination is null)
            return ValidationResult.Fail("Enter where this travel leg arrives.", "destination");

        // Booking details are welcome on every travel mode but never demanded: a leg can be
        // planned long before it is booked. Only a value that was actually supplied is checked.
        if (shape.TravelCost is { } cost)
        {
            if (cost < 0)
                return ValidationResult.Fail("Travel cost cannot be negative.", "travelCost");
            if (cost > TravelCostMax)
                return ValidationResult.Fail("Travel cost is too large.", "travelCost");
            if (decimal.Round(cost, 2) != cost)
                return ValidationResult.Fail("Enter a travel cost with up to two decimal places.", "travelCost");
        }

        if (shape.ConfirmationCode is { Length: > ConfirmationCodeMaxLength })
            return ValidationResult.Fail($"Confirmation/Reservation Code must be {ConfirmationCodeMaxLength} characters or fewer.", "confirmationCode");

        return ValidationResult.Success;
    }

    private static DateTimeOffset ToInstant(DateTime local, TimeZoneInfo timeZone)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(unspecifiedLocal);
        return new DateTimeOffset(unspecifiedLocal, offset).ToUniversalTime();
    }
}
