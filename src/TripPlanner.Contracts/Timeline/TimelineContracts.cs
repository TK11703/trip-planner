using TripPlanner.Contracts.TripItems;

namespace TripPlanner.Contracts.Timeline;

public sealed record TripTimelineResponse(
    Guid TripId,
    DateOnly StartDate,
    DateOnly EndDate,
    int SlotMinutes,
    IReadOnlyList<TimelineLeg> Legs,
    IReadOnlyList<TimelineItem> UnassignedItems);

public sealed record TimelineLeg(
    Guid TripLegId,
    string Title,
    string? Origin,
    string? Destination,
    DateTime StartLocal,
    string StartTimeZoneId,
    string? StartTimeZoneLabel,
    DateTime EndLocal,
    string EndTimeZoneId,
    string? EndTimeZoneLabel,
    int SortOrder,
    IReadOnlyList<TimelineItem> Items,
    decimal EstimatedCostTotal = 0m,
    string? LegKind = null,
    string? TransportationMode = null,
    decimal? TravelCost = null,
    string? ConfirmationCode = null)
{
    /// <summary>Derived from kind and mode; drives whether the timeline offers item entry on this row.</summary>
    public bool CanContainItems => TripLegEligibility.CanContainItems(LegKind, TransportationMode);

    public string ModeLabel => TransportationModes.Label(TransportationMode);
}

public sealed record TimelineItem(
    Guid TrackedItemId,
    Guid? TripLegId,
    string ItemType,
    string Title,
    string? Location,
    DateTime StartLocal,
    string StartTimeZoneId,
    DateTimeOffset StartsAt,
    DateTime? EndLocal,
    string? EndTimeZoneId,
    DateTimeOffset? EndsAt,
    string DisplayColor,
    bool StartsOutsideLeg,
    bool EndsOutsideLeg,
    int SortOrder,
    decimal? EstimatedCost = null);
