namespace TripPlanner.Contracts.Audit;

public static class AuditOperations
{
    public const string TripRead = "trip.read";
    public const string TripCreate = "trip.create";
    public const string TripUpdate = "trip.update";
    public const string TripDelete = "trip.delete";
    public const string TripLegCreate = "trip-leg.create";
    public const string TripLegUpdate = "trip-leg.update";
    public const string TripLegDelete = "trip-leg.delete";
    public const string TrackedItemCreate = "tracked-item.create";
    public const string TrackedItemUpdate = "tracked-item.update";
    public const string TrackedItemDelete = "tracked-item.delete";
    public const string TimelineRead = "timeline.read";
    public const string AccessDenied = "access.denied";
}

public static class AuditResults
{
    public const string Success = "success";
    public const string Denied = "denied";
    public const string Unauthenticated = "unauthenticated";
    public const string ValidationFailed = "validation-failed";
    public const string Error = "error";
}

public sealed record AuditEvent(
    Guid AuditEventId,
    string? UserId,
    string Operation,
    string ResourceType,
    string? ResourceId,
    string Result,
    DateTimeOffset OccurredAtUtc);
