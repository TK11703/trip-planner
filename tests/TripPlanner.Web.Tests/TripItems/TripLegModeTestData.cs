using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Web.Tests.TripItems;

/// <summary>
/// Leg fixtures for the feature 025 component tests. The rendering rules turn only on kind and
/// mode, so these builders vary those and hold everything else constant.
/// </summary>
internal static class TripLegModeTestData
{
    public static readonly DateTime LegStart = new(2026, 7, 11, 8, 0, 0);
    public static readonly DateTime LegEnd = new(2026, 7, 12, 8, 0, 0);

    public static readonly TheoryData<string> RestrictedModes = new()
    {
        TransportationModes.Flight,
        TransportationModes.Train,
        TransportationModes.Bus,
        TransportationModes.Boat,
    };

    public static TripLegDto TravelLeg(
        string mode,
        Guid? legId = null,
        Guid? tripId = null,
        string title = "Travel leg",
        decimal? travelCost = null,
        string? confirmationCode = null) => new(
        legId ?? Guid.NewGuid(), tripId ?? Guid.NewGuid(), title, "Paris", "Chicago",
        LegStart, "UTC", "UTC", LegEnd, "UTC", "UTC", null, 0,
        TripLegKinds.Travel, mode, travelCost, confirmationCode);

    public static TripLegDto StayLeg(
        Guid? legId = null,
        Guid? tripId = null,
        string title = "Stay leg") => new(
        legId ?? Guid.NewGuid(), tripId ?? Guid.NewGuid(), title, null, "Chicago",
        LegStart, "UTC", "UTC", LegEnd, "UTC", "UTC", null, 0,
        TripLegKinds.Stay, null, null, null);

    public static TimelineLeg TimelineTravelLeg(
        string mode,
        Guid? legId = null,
        string title = "Travel leg",
        decimal? travelCost = null,
        string? confirmationCode = null) => new(
        legId ?? Guid.NewGuid(), title, "Paris", "Chicago",
        LegStart, "UTC", "UTC", LegEnd, "UTC", "UTC", 0, Array.Empty<TimelineItem>(), 0m,
        TripLegKinds.Travel, mode, travelCost, confirmationCode);

    public static TimelineLeg TimelineStayLeg(Guid? legId = null, string title = "Stay leg") => new(
        legId ?? Guid.NewGuid(), title, null, "Chicago",
        LegStart, "UTC", "UTC", LegEnd, "UTC", "UTC", 0, Array.Empty<TimelineItem>(), 0m,
        TripLegKinds.Stay, null, null, null);

    public static TripTimelineResponse Timeline(Guid tripId, params TimelineLeg[] legs) => new(
        tripId, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 18), 30, legs, Array.Empty<TimelineItem>());
}
