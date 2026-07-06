namespace TripPlanner.Contracts.Timeline;

public sealed record TimelineResponse(Guid TripId, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<TimelineEvent> Events);

public sealed record TimelineEvent(
    string Id,
    string SourceType,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset? End,
    string CalendarStart,
    string? CalendarEnd,
    string? StartTimeZoneId,
    string? StartTimeZoneLabel,
    string? EndTimeZoneId,
    string? EndTimeZoneLabel,
    bool AllDay,
    int DisplayOrder,
    IReadOnlyDictionary<string, string?>? Metadata = null);
