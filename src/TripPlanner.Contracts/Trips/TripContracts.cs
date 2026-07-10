namespace TripPlanner.Contracts.Trips;

public sealed record UserAccount(string UserId, string? DisplayName, string? Email, DateTimeOffset CreatedAtUtc, DateTimeOffset LastSeenAtUtc);

/// <summary>The caller's relationship to a trip. Owner holds full control; collaborators can edit
/// itinerary content; viewers are read-only.</summary>
public enum TripAccessLevel
{
    Owner = 0,
    Collaborator = 1,
    Viewer = 2
}

public sealed record TripSummary(
    Guid TripId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset UpdatedAtUtc,
    int ItemCount,
    TripAccessLevel AccessLevel = TripAccessLevel.Owner,
    bool IsOwner = true);

public sealed record TripListResponse(
    IReadOnlyList<TripSummary> Trips,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>A person granted access to a trip and their current access level.</summary>
public sealed record TripShareMember(
    string UserId,
    string? DisplayName,
    string? Email,
    TripAccessLevel AccessLevel,
    DateTimeOffset UpdatedAtUtc);

/// <summary>A minimal tenant directory user projection returned to the share dialog.</summary>
public sealed record DirectoryUserResult(
    string UserId,
    string? DisplayName,
    string? Email,
    string? UserPrincipalName);

/// <summary>Owner request to create or update a share for a tenant user. AccessLevel must be
/// Collaborator or Viewer.</summary>
public sealed record UpsertTripShareRequest(
    string UserId,
    string? DisplayName,
    string? Email,
    TripAccessLevel AccessLevel);

/// <summary>Owner request to change an existing member's access level.</summary>
public sealed record UpdateTripShareAccessRequest(
    TripAccessLevel AccessLevel);

public sealed record TripDetail(
    Guid TripId,
    string Name,
    string? Description,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<TripLegDto> Legs,
    IReadOnlyList<TrackedItemDto> TrackedItems,
    TripAccessLevel AccessLevel = TripAccessLevel.Owner,
    bool IsOwner = true,
    IReadOnlyList<TripShareMember>? SharedPeople = null,
    decimal EstimatedCostTotal = 0m);

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
    int SortOrder,
    decimal? EstimatedCost = null);
