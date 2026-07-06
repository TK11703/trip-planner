namespace TripPlanner.Contracts.TripItems;

public sealed record CreateTripLegRequest(string Title, string? Origin, string? Destination, DateTime StartLocal, string StartTimeZoneId, DateTime EndLocal, string EndTimeZoneId, string? Notes);
public sealed record UpdateTripLegRequest(string Title, string? Origin, string? Destination, DateTime StartLocal, string StartTimeZoneId, DateTime EndLocal, string EndTimeZoneId, string? Notes);

public sealed record TripLegDefaultsResponse(string StartTimeZoneId, string EndTimeZoneId, string Source);

public static class TrackedItemTypes
{
    public const string Event = "event";
    public const string Reservation = "reservation";
    public const string Activity = "activity";
    public const string Reminder = "reminder";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { Event, Reservation, Activity, Reminder };
}

public static class TrackedItemColors
{
    public const string Default = "slate";

    public static readonly IReadOnlyList<string> All = new[]
    { "slate", "teal", "blue", "green", "gold", "orange", "red", "purple" };

    private static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? color) => !string.IsNullOrWhiteSpace(color) && Allowed.Contains(color);

    public static string Normalize(string? color) => IsValid(color) ? color!.Trim().ToLowerInvariant() : Default;
}

public sealed record CreateTrackedItemRequest(Guid TripLegId, string ItemType, string Title, string? Location, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string DisplayColor, string? ConfirmationCode, string? Notes);
public sealed record UpdateTrackedItemRequest(Guid TripLegId, string ItemType, string Title, string? Location, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string DisplayColor, string? ConfirmationCode, string? Notes);
