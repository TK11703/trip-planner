namespace TripPlanner.Contracts.TripItems;

public sealed record CreateTripLegRequest(string Title, string? Origin, string? Destination, DateTimeOffset StartAt, DateTimeOffset? EndAt, string? Notes);
public sealed record UpdateTripLegRequest(string Title, string? Origin, string? Destination, DateTimeOffset StartAt, DateTimeOffset? EndAt, string? Notes);

public static class TrackedItemTypes
{
    public const string Event = "event";
    public const string Reservation = "reservation";
    public const string Activity = "activity";
    public const string Reminder = "reminder";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { Event, Reservation, Activity, Reminder };
}

public sealed record CreateTrackedItemRequest(string ItemType, string Title, string? Location, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? ConfirmationCode, string? Notes);
public sealed record UpdateTrackedItemRequest(string ItemType, string Title, string? Location, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string? ConfirmationCode, string? Notes);
