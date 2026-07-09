namespace TripPlanner.Contracts.Notifications;

/// <summary>Whether a notification is purely informational or requires an action.</summary>
public enum NotificationKind
{
    Awareness = 0,
    Actionable = 1
}

/// <summary>Whether a notification targets only a person or a person in the context of a trip.</summary>
public enum NotificationTargetType
{
    Person = 0,
    Trip = 1
}

/// <summary>Completion state for an actionable notification.</summary>
public enum NotificationActionStatus
{
    NotApplicable = 0,
    Pending = 1,
    Completed = 2
}

/// <summary>Email delivery state associated with a notification.</summary>
public enum NotificationEmailStatus
{
    NotRequested = 0,
    Pending = 1,
    Sent = 2,
    Failed = 3,
    Suppressed = 4
}

/// <summary>The person who completed an actionable notification.</summary>
public sealed record NotificationCompletedBy(string UserId, string? DisplayName);

/// <summary>A single notification as shown to its recipient.</summary>
public sealed record NotificationResponse(
    Guid NotificationId,
    string Category,
    NotificationKind Kind,
    NotificationTargetType TargetType,
    Guid? RelatedTripId,
    string? RelatedTripName,
    string Title,
    string Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc,
    NotificationActionStatus ActionStatus,
    DateTimeOffset? CompletedAtUtc,
    NotificationCompletedBy? CompletedBy,
    NotificationEmailStatus EmailDeliveryStatus,
    bool CanOpenTrip);

/// <summary>A page of the recipient's notifications, newest first.</summary>
public sealed record NotificationListResponse(
    IReadOnlyList<NotificationResponse> Items,
    string? NextCursor = null);

/// <summary>The recipient's current unread notification count.</summary>
public sealed record NotificationCountResponse(int UnreadCount);

/// <summary>Result of marking a single notification read.</summary>
public sealed record MarkNotificationReadResponse(Guid NotificationId, DateTimeOffset ReadAtUtc);

/// <summary>Result of marking all notifications read.</summary>
public sealed record MarkAllNotificationsReadResponse(int MarkedReadCount, DateTimeOffset ReadAtUtc);

/// <summary>Result of completing an actionable notification.</summary>
public sealed record CompleteNotificationResponse(
    Guid NotificationId,
    NotificationActionStatus ActionStatus,
    DateTimeOffset CompletedAtUtc,
    NotificationCompletedBy CompletedBy);

/// <summary>A recipient's delivery settings for one notification category.</summary>
public sealed record NotificationPreferenceResponse(
    string Category,
    string DisplayName,
    bool InAppEnabled,
    bool EmailEnabled,
    DateTimeOffset? UpdatedAtUtc = null);

/// <summary>All of a recipient's notification preferences.</summary>
public sealed record NotificationPreferencesResponse(IReadOnlyList<NotificationPreferenceResponse> Categories);

/// <summary>Request to change delivery settings for one category.</summary>
public sealed record UpdateNotificationPreferenceRequest(bool InAppEnabled, bool EmailEnabled);

/// <summary>Known notification categories and their user-facing names and defaults.</summary>
public static class NotificationCategories
{
    public const string ItineraryChanges = "ItineraryChanges";
    public const string TripSharing = "TripSharing";

    public static IReadOnlyList<NotificationCategoryDefinition> All { get; } = new[]
    {
        new NotificationCategoryDefinition(ItineraryChanges, "Itinerary changes", true, true),
        new NotificationCategoryDefinition(TripSharing, "Trip sharing", true, true)
    };

    public static bool IsKnown(string category)
        => All.Any(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));

    public static NotificationCategoryDefinition Resolve(string category)
        => All.FirstOrDefault(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase))
            ?? new NotificationCategoryDefinition(category, category, true, true);
}

/// <summary>A category definition with its display name and default channel settings.</summary>
public sealed record NotificationCategoryDefinition(
    string Category,
    string DisplayName,
    bool DefaultInAppEnabled,
    bool DefaultEmailEnabled);
