using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Turns the recognizer's booking category into a transportation mode and a proposed outcome.
///
/// Recognition answers in its own vocabulary ("flight", "hotel", "car_rental"). The leg model
/// speaks <see cref="TransportationModes"/>. This is the single place the two meet, so the same
/// booking always proposes the same thing (FR-036).
///
/// A car rental becomes a Car travel leg rather than a reservation item: Car is the one travel
/// mode that still accepts items, so the leg captures the route without giving up the ability to
/// hold stops along the way, and a rental confirmation almost always states both a pickup and a
/// return. The traveler can still override it to an item (FR-035, FR-037).
/// </summary>
public static class TransportationModeInterpreter
{
    /// <summary>
    /// The mode a recognized category implies, or null when the booking is not transportation.
    /// Unknown categories yield null, so an unfamiliar answer becomes an item rather than a leg
    /// built on a guess.
    /// </summary>
    public static string? FromRecognizedType(string? itemType) => Normalize(itemType) switch
    {
        "flight" or "plane" or "air" => TransportationModes.Flight,
        "train" or "rail" => TransportationModes.Train,
        "bus" or "coach" => TransportationModes.Bus,
        "boat" or "ferry" or "cruise" => TransportationModes.Boat,
        "car_rental" or "car" or "rental_car" => TransportationModes.Car,
        _ => null
    };

    /// <summary>
    /// A mode the recognizer stated outright, kept only when the leg model supports it. An
    /// unsupported answer is dropped rather than coerced, so the traveler is asked instead of
    /// being given a mode the booking never mentioned.
    /// </summary>
    public static string? FromRecognizedMode(string? mode) =>
        TransportationModes.IsValid(mode) ? TransportationModes.Normalize(mode) : null;

    /// <summary>
    /// The mode a draft should carry, preferring an explicitly stated mode over one inferred from
    /// the booking category.
    /// </summary>
    public static string? Resolve(string? itemType, string? mode)
        => FromRecognizedMode(mode) ?? FromRecognizedType(itemType);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant().Replace(' ', '_');
}

/// <summary>
/// Decides whether a recognized booking is proposed as a trip leg or as a tracked item.
///
/// Only a transportation booking proposes a leg. Everything else — a hotel, a dinner
/// reservation, a museum ticket — keeps the item behaviour it has always had (FR-001, FR-004).
/// </summary>
public static class DraftOutcomeClassifier
{
    /// <summary>
    /// Returns the persisted outcome value for a recognized booking. A booking that names a
    /// supported transportation mode proposes a leg; anything else proposes an item.
    /// </summary>
    public static string Classify(string? itemType, string? mode)
        => TransportationModeInterpreter.Resolve(itemType, mode) is null
            ? DraftOutcomes.Item
            : DraftOutcomes.Leg;
}
