namespace TripPlanner.Contracts.Timeline;

public sealed record TimelineResponse(Guid TripId, DateOnly StartDate, DateOnly EndDate, IReadOnlyList<TimelineEvent> Events);

public sealed record TimelineEvent(
    string Id,
    string SourceType,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset? End,
    bool AllDay,
    int DisplayOrder,
    IReadOnlyDictionary<string, string?>? Metadata = null);
