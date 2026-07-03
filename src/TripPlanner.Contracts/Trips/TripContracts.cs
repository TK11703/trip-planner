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
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    string? Notes,
    int SortOrder);

public sealed record TrackedItemDto(
    Guid TrackedItemId,
    Guid TripId,
    string ItemType,
    string Title,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? ConfirmationCode,
    string? Notes,
    int SortOrder);
