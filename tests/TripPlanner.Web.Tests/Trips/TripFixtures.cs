using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Web.Tests.Trips;

// Shared TripDetail fixtures for the printable trip tests.
internal static class TripFixtures
{
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

    private static TripLegDto Leg(string title, DateTime start, int sortOrder, string? origin = null, string? destination = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), title, origin, destination, start, "America/New_York", "America/New_York",
            start.AddHours(3), "America/New_York", "America/New_York", null, sortOrder);

    private static TrackedItemDto Item(Guid? legId, string title, DateTime start, int sortOrder,
        string? location = null, decimal? cost = null, string? confirmation = null, string? notes = null, DateTime? end = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), legId, TrackedItemTypes.Event, title, location, start, "America/New_York",
            new DateTimeOffset(start, TimeSpan.FromHours(-4)), end,
            end is null ? null : "America/New_York",
            end is null ? null : new DateTimeOffset(end.Value, TimeSpan.FromHours(-4)),
            TrackedItemColors.Default, confirmation, notes, sortOrder, cost);

    private static TripDetail Trip(string name, string? description, IReadOnlyList<TripLegDto> legs, IReadOnlyList<TrackedItemDto> items) =>
        new(Guid.NewGuid(), name, description, new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, legs, items);
}
