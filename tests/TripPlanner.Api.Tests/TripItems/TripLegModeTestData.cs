using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Api.Tests.TripItems;

/// <summary>
/// Builders for the leg shapes feature 025 cares about. Every case in this feature is "the same
/// leg, but a different kind or mode", so the tests say only what differs and let this file hold
/// the dates, timezones, and trip window that never matter to the assertion.
/// </summary>
internal static class TripLegModeTestData
{
    public static readonly DateOnly TripStart = new(2026, 7, 10);
    public static readonly DateOnly TripEnd = new(2026, 7, 18);
    public static readonly DateTime LegStart = new(2026, 7, 11, 8, 0, 0);
    public static readonly DateTime LegEnd = new(2026, 7, 12, 8, 0, 0);

    /// <summary>The restricted modes: on each of these the traveler is a passenger.</summary>
    public static readonly TheoryData<string> RestrictedModes = new()
    {
        TransportationModes.Flight,
        TransportationModes.Train,
        TransportationModes.Bus,
        TransportationModes.Boat,
    };

    public static CreateTripLegRequest CreateTravel(
        string mode,
        string? origin = "Paris",
        string? destination = "Chicago",
        decimal? travelCost = null,
        string? confirmationCode = null) => new(
        "Leg", origin, destination, LegStart, "UTC", LegEnd, "UTC", null,
        TripLegKinds.Travel, mode, travelCost, confirmationCode);

    public static CreateTripLegRequest CreateStay(string? destination = null) => new(
        "Leg", null, destination, LegStart, "UTC", LegEnd, "UTC", null,
        TripLegKinds.Stay, null, null, null);

    public static UpdateTripLegRequest ToUpdate(this CreateTripLegRequest request) => new(
        request.Title, request.Origin, request.Destination, request.StartLocal, request.StartTimeZoneId,
        request.EndLocal, request.EndTimeZoneId, request.Notes,
        request.LegKind, request.TransportationMode, request.TravelCost, request.ConfirmationCode);

    public static TripLegDto TravelLeg(Guid tripId, Guid legId, string mode, decimal? travelCost = null, string? confirmationCode = null) => new(
        legId, tripId, "Leg", "Paris", "Chicago", LegStart, "UTC", "UTC", LegEnd, "UTC", "UTC", null, 0,
        TripLegKinds.Travel, mode, travelCost, confirmationCode);

    public static TripLegDto StayLeg(Guid tripId, Guid legId) => new(
        legId, tripId, "Leg", null, null, LegStart, "UTC", "UTC", LegEnd, "UTC", "UTC", null, 0,
        TripLegKinds.Stay, null, null, null);

    public static TripDetail TripWith(Guid tripId, params TripLegDto[] legs) => new(
        tripId, "Trip", null, TripStart, TripEnd, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        legs, Array.Empty<TrackedItemDto>());
}
