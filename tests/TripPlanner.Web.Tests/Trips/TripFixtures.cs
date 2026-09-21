using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Web.Tests.Trips;

// Shared TripDetail fixtures for the printable trip tests.
internal static class TripFixtures
{
    public static readonly Guid RepresentativeTripId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid ArrivalLegId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid EmptyLegId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid DepartureLegId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid WalkItemId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid DinnerItemId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid UnassignedItemId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid MissingLegItemId = Guid.Parse("30000000-0000-0000-0000-000000000004");

    public static TripDetail Representative()
    {
        var arrival = Leg(ArrivalLegId, "Arrival", new DateTime(2026, 7, 14, 8, 0, 0), sortOrder: 0,
            origin: "Seattle", destination: "Tokyo", legKind: TripLegKinds.Travel,
            transportationMode: TransportationModes.Car);
        var empty = Leg(EmptyLegId, "Free day", new DateTime(2026, 7, 15, 8, 0, 0), sortOrder: 1,
            legKind: TripLegKinds.Stay);
        var departure = Leg(DepartureLegId, "Departure", new DateTime(2026, 7, 16, 8, 0, 0), sortOrder: 2,
            origin: "Tokyo", destination: "Seattle", legKind: TripLegKinds.Travel,
            transportationMode: TransportationModes.Flight);
        var missingLegId = Guid.Parse("20000000-0000-0000-0000-000000000099");

        var walk = Item(WalkItemId, ArrivalLegId, "Walk", new DateTime(2026, 7, 14, 9, 30, 0), sortOrder: 0);
        var dinner = Item(DinnerItemId, ArrivalLegId, "Dinner", new DateTime(2026, 7, 14, 19, 0, 0), sortOrder: 1,
            location: "Ginza", cost: 80m, confirmation: "ABC123", notes: "Table for two",
            end: new DateTime(2026, 7, 14, 21, 0, 0));
        var unassigned = Item(UnassignedItemId, null, "Pack", new DateTime(2026, 7, 13, 18, 0, 0), sortOrder: 0);
        var missingLeg = Item(MissingLegItemId, missingLegId, "Review transfer", new DateTime(2026, 7, 15, 12, 0, 0), sortOrder: 1);

        return Trip(RepresentativeTripId, "Japan 2026", "Cherry blossoms",
            new[] { departure, empty, arrival },
            new[] { dinner, missingLeg, walk, unassigned });
    }

    public static TripDetail Populated()
    {
        var arrival = Leg("Arrival", new DateTime(2026, 7, 14, 8, 0, 0), sortOrder: 0, origin: "Seattle", destination: "Tokyo");
        var departure = Leg("Departure", new DateTime(2026, 7, 16, 8, 0, 0), sortOrder: 1, origin: "Tokyo", destination: "Seattle");

        var walk = Item(arrival.TripLegId, "Walk", new DateTime(2026, 7, 14, 9, 30, 0), sortOrder: 0);
        var dinner = Item(arrival.TripLegId, "Dinner", new DateTime(2026, 7, 14, 19, 0, 0), sortOrder: 1,
            location: "Ginza", cost: 80m, confirmation: "ABC123", notes: "Table for two", end: new DateTime(2026, 7, 14, 21, 0, 0));
        var flightHome = Item(departure.TripLegId, "Flight home", new DateTime(2026, 7, 16, 10, 0, 0), sortOrder: 0);

        return Trip("Japan 2026", "Cherry blossoms",
            new[] { departure, arrival }, // intentionally out of order to prove chronological sorting
            new[] { dinner, walk, flightHome });
    }

    public static TripDetail Empty() =>
        Trip("Empty trip", null, Array.Empty<TripLegDto>(), Array.Empty<TrackedItemDto>());

    /// <summary>A trip whose single leg is classified travel, optionally with booking details.</summary>
    public static TripDetail WithTravelLeg(string mode, decimal? travelCost = null, string? confirmationCode = null)
    {
        var leg = Leg("Getting there", new DateTime(2026, 7, 14, 8, 0, 0), sortOrder: 0,
            origin: "Seattle", destination: "Tokyo",
            legKind: TripLegKinds.Travel, transportationMode: mode,
            travelCost: travelCost, confirmationCode: confirmationCode);

        return Trip("Japan 2026", null, new[] { leg }, Array.Empty<TrackedItemDto>());
    }

    /// <summary>A trip whose single leg is a stay, so it carries no travel-only details.</summary>
    public static TripDetail WithStayLeg()
    {
        var leg = Leg("Hotel Kabuki", new DateTime(2026, 7, 14, 15, 0, 0), sortOrder: 0,
            legKind: TripLegKinds.Stay);

        return Trip("Japan 2026", null, new[] { leg }, Array.Empty<TrackedItemDto>());
    }

    private static TripLegDto Leg(string title, DateTime start, int sortOrder, string? origin = null, string? destination = null,
        string? legKind = null, string? transportationMode = null, decimal? travelCost = null, string? confirmationCode = null) =>
        Leg(Guid.NewGuid(), title, start, sortOrder, origin, destination, legKind, transportationMode, travelCost, confirmationCode);

    private static TripLegDto Leg(Guid legId, string title, DateTime start, int sortOrder, string? origin = null, string? destination = null,
        string? legKind = null, string? transportationMode = null, decimal? travelCost = null, string? confirmationCode = null) =>
        new(legId, RepresentativeTripId, title, origin, destination, start, "America/New_York", "America/New_York",
            start.AddHours(3), "America/New_York", "America/New_York", null, sortOrder,
            legKind, transportationMode, travelCost, confirmationCode);

    private static TrackedItemDto Item(Guid? legId, string title, DateTime start, int sortOrder,
        string? location = null, decimal? cost = null, string? confirmation = null, string? notes = null, DateTime? end = null) =>
        Item(Guid.NewGuid(), legId, title, start, sortOrder, location, cost, confirmation, notes, end);

    private static TrackedItemDto Item(Guid itemId, Guid? legId, string title, DateTime start, int sortOrder,
        string? location = null, decimal? cost = null, string? confirmation = null, string? notes = null, DateTime? end = null) =>
        new(itemId, RepresentativeTripId, legId, TrackedItemTypes.Event, title, location, start, "America/New_York",
            new DateTimeOffset(start, TimeSpan.FromHours(-4)), end,
            end is null ? null : "America/New_York",
            end is null ? null : new DateTimeOffset(end.Value, TimeSpan.FromHours(-4)),
            TrackedItemColors.Default, confirmation, notes, sortOrder, cost);

    private static TripDetail Trip(string name, string? description, IReadOnlyList<TripLegDto> legs, IReadOnlyList<TrackedItemDto> items) =>
        Trip(Guid.NewGuid(), name, description, legs, items);

    private static TripDetail Trip(Guid tripId, string name, string? description, IReadOnlyList<TripLegDto> legs, IReadOnlyList<TrackedItemDto> items) =>
        new(tripId, name, description, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, legs, items);
}
