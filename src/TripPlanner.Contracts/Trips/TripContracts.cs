namespace TripPlanner.Contracts.Trips;

public sealed record UserAccount(string UserId, string? DisplayName, string? Email, DateTimeOffset CreatedAtUtc, DateTimeOffset LastSeenAtUtc);

public sealed record TripSummary(
    Guid TripId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset UpdatedAtUtc,
    int ItemCount);

public sealed record TripListResponse(
    IReadOnlyList<TripSummary> Trips,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record TripDetail(
    Guid TripId,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<TripLegDto> Legs,
    IReadOnlyList<TrackedItemDto> TrackedItems);

public sealed record TripLegDto(
    Guid TripLegId,
    Guid TripId,
    string Title,
    string? Origin,
    string? Destination,
    DateTime StartLocal,
    string StartTimeZoneId,
    string? StartTimeZoneLabel,
    DateTime EndLocal,
    string EndTimeZoneId,
    string? EndTimeZoneLabel,
    string? Notes,
    int SortOrder);

public sealed record TrackedItemDto(
    Guid TrackedItemId,
    Guid TripId,
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
    string? ConfirmationCode,
    string? Notes,
    int SortOrder);
