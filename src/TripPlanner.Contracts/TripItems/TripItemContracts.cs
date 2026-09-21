namespace TripPlanner.Contracts.TripItems;

/// <summary>What a leg represents: time spent at a destination, or movement between two places.</summary>
public static class TripLegKinds
{
    public const string Stay = "stay";
    public const string Travel = "travel";

    public static readonly IReadOnlyList<string> All = new[] { Stay, Travel };

    private static readonly HashSet<string> Allowed = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? kind) => !string.IsNullOrWhiteSpace(kind) && Allowed.Contains(kind.Trim());

    public static bool IsTravel(string? kind) =>
        !string.IsNullOrWhiteSpace(kind) && string.Equals(kind.Trim(), Travel, StringComparison.OrdinalIgnoreCase);

    /// <summary>Lower-cases a supplied kind. Anything unrecognized becomes <see cref="Stay"/>, the
    /// classification that carries no travel-only data.</summary>
    public static string Normalize(string? kind) => IsTravel(kind) ? Travel : Stay;
}

/// <summary>How a Travel leg moves the traveler. Only <see cref="Car"/> leaves stop timing under the
/// traveler's control, which is what decides item eligibility.</summary>
public static class TransportationModes
{
    public const string Flight = "flight";
    public const string Train = "train";
    public const string Bus = "bus";
    public const string Boat = "boat";
    public const string Car = "car";

    public static readonly IReadOnlyList<string> All = new[] { Flight, Train, Bus, Boat, Car };

    private static readonly HashSet<string> Allowed = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? mode) => !string.IsNullOrWhiteSpace(mode) && Allowed.Contains(mode.Trim());

    public static string? Normalize(string? mode) => IsValid(mode) ? mode!.Trim().ToLowerInvariant() : null;

    public static bool IsCar(string? mode) =>
        !string.IsNullOrWhiteSpace(mode) && string.Equals(mode.Trim(), Car, StringComparison.OrdinalIgnoreCase);

    public static string Label(string? mode) => Normalize(mode) switch
    {
        Flight => "Flight",
        Train => "Train",
        Bus => "Bus",
        Boat => "Boat",
        Car => "Car",
        _ => string.Empty
    };
}

/// <summary>The single rule deciding where itinerary items may live, shared by the API, the database
/// guard rails, and every Blazor surface so they cannot drift apart.</summary>
public static class TripLegEligibility
{
    /// <summary>A Stay always accepts items. A Travel leg accepts them only by car, because a
    /// passenger on a flight, train, bus, or boat cannot schedule their own stops.</summary>
    public static bool CanContainItems(string? legKind, string? transportationMode) =>
        !TripLegKinds.IsTravel(legKind) || TransportationModes.IsCar(transportationMode);
}

/// <summary>The persisted shape of a leg write, resolved once and reused by validation and
/// persistence so a leg is never checked as one thing and stored as another.</summary>
public readonly record struct TripLegShape(
    string LegKind,
    string? TransportationMode,
    string? Origin,
    string? Destination,
    decimal? TravelCost,
    string? ConfirmationCode)
{
    public bool IsTravel => TripLegKinds.IsTravel(LegKind);

    public bool CanContainItems => TripLegEligibility.CanContainItems(LegKind, TransportationMode);

    /// <summary>
    /// Normalizes a leg request into what will actually be stored. A request that omits the
    /// classification is read exactly the way existing rows were migrated — an origin means travel
    /// by car — so older clients keep working without inventing a different rule. Travel-only
    /// values are dropped for a Stay rather than kept as hidden data.
    /// </summary>
    public static TripLegShape Resolve(string? legKind, string? transportationMode, string? origin, string? destination, decimal? travelCost, string? confirmationCode)
    {
        var kindSupplied = TripLegKinds.IsValid(legKind);
        var kind = kindSupplied
            ? TripLegKinds.Normalize(legKind)
            : string.IsNullOrWhiteSpace(origin) ? TripLegKinds.Stay : TripLegKinds.Travel;

        if (kind == TripLegKinds.Stay)
        {
            return new TripLegShape(TripLegKinds.Stay, null, null, null, null, null);
        }

        // Only an inferred classification gets a default mode; an explicit Travel request that
        // names no mode is left invalid for the validator to report.
        var mode = TransportationModes.Normalize(transportationMode)
            ?? (kindSupplied ? null : TransportationModes.Car);

        var trimmedCode = string.IsNullOrWhiteSpace(confirmationCode) ? null : confirmationCode.Trim();
        var trimmedOrigin = string.IsNullOrWhiteSpace(origin) ? null : origin.Trim();
        var trimmedDestination = string.IsNullOrWhiteSpace(destination) ? null : destination.Trim();

        return new TripLegShape(TripLegKinds.Travel, mode, trimmedOrigin, trimmedDestination, travelCost, trimmedCode);
    }

    public static TripLegShape Resolve(CreateTripLegRequest request) =>
        Resolve(request.LegKind, request.TransportationMode, request.Origin, request.Destination, request.TravelCost, request.ConfirmationCode);

    public static TripLegShape Resolve(UpdateTripLegRequest request) =>
        Resolve(request.LegKind, request.TransportationMode, request.Origin, request.Destination, request.TravelCost, request.ConfirmationCode);
}

public sealed record CreateTripLegRequest(string Title, string? Origin, string? Destination, DateTime StartLocal, string StartTimeZoneId, DateTime EndLocal, string EndTimeZoneId, string? Notes, string? LegKind = null, string? TransportationMode = null, decimal? TravelCost = null, string? ConfirmationCode = null);
public sealed record UpdateTripLegRequest(string Title, string? Origin, string? Destination, DateTime StartLocal, string StartTimeZoneId, DateTime EndLocal, string EndTimeZoneId, string? Notes, string? LegKind = null, string? TransportationMode = null, decimal? TravelCost = null, string? ConfirmationCode = null);

public sealed record TripLegDefaultsResponse(string StartTimeZoneId, string EndTimeZoneId, string Source);

public static class TrackedItemTypes
{
    public const string Event = "event";
    public const string Reservation = "reservation";
    public const string Activity = "activity";
    public const string Reminder = "reminder";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { Event, Reservation, Activity, Reminder };
}

public static class TrackedItemColors
{
    public const string Default = "slate";

    public static readonly IReadOnlyList<string> All = new[]
    { "slate", "teal", "blue", "green", "gold", "orange", "red", "purple",
      "pink", "indigo", "cyan", "lime", "amber", "brown", "magenta", "navy" };

    private static readonly HashSet<string> Allowed =
        new HashSet<string>(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? color) => !string.IsNullOrWhiteSpace(color) && Allowed.Contains(color);

    public static string Normalize(string? color) => IsValid(color) ? color!.Trim().ToLowerInvariant() : Default;
}

public sealed record CreateTrackedItemRequest(Guid? TripLegId, string ItemType, string Title, string? Location, DateTime StartLocal, string StartTimeZoneId, DateTime? EndLocal, string? EndTimeZoneId, string DisplayColor, string? ConfirmationCode, string? Notes, decimal? EstimatedCost = null);
public sealed record UpdateTrackedItemRequest(Guid? TripLegId, string ItemType, string Title, string? Location, DateTime StartLocal, string StartTimeZoneId, DateTime? EndLocal, string? EndTimeZoneId, string DisplayColor, string? ConfirmationCode, string? Notes, decimal? EstimatedCost = null);
