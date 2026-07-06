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
    IReadOnlyList<TimelineItem> Items);

public sealed record TimelineItem(
    Guid TrackedItemId,
    Guid? TripLegId,
    string ItemType,
    string Title,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string DisplayColor,
    bool StartsOutsideLeg,
    bool EndsOutsideLeg,
    int SortOrder);
