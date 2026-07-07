using TripPlanner.Contracts.Notifications;

namespace TripPlanner.Database.Notifications;

/// <summary>A recipient-owned notification as stored, with mapped enum values and optional trip name.</summary>
public sealed record NotificationRecord(
    Guid NotificationId,
    string RecipientUserId,
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
    string? CompletedByUserId,
    string? CompletedByDisplayName,
    NotificationEmailStatus EmailDeliveryStatus);

/// <summary>A new notification to create for a single recipient.</summary>
public sealed record NewNotification(
    string RecipientUserId,
    string Category,
    NotificationKind Kind,
    NotificationTargetType TargetType,
    Guid? RelatedTripId,
    string Title,
    string Message,
    string SourceEventKey,
    string? RecipientEmail = null);

/// <summary>A recipient's stored delivery settings for one category.</summary>
public sealed record NotificationPreferenceRecord(
    string UserId,
    string Category,
    bool InAppEnabled,
    bool EmailEnabled,
    DateTimeOffset UpdatedAtUtc);

public interface INotificationRepository
{
    Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationRecord>> GetListAsync(string recipientUserId, int limit, CancellationToken ct = default);
    Task<NotificationRecord?> GetAsync(string recipientUserId, Guid notificationId, CancellationToken ct = default);
    Task<NotificationRecord?> CreateAsync(NewNotification notification, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task<DateTimeOffset?> MarkReadAsync(string recipientUserId, Guid notificationId, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(string recipientUserId, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task<bool> DeleteAsync(string recipientUserId, Guid notificationId, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task<NotificationRecord?> CompleteAsync(string recipientUserId, Guid notificationId, string completedByUserId, string? completedByDisplayName, DateTimeOffset nowUtc, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationPreferenceRecord>> GetPreferencesAsync(string userId, CancellationToken ct = default);
    Task<NotificationPreferenceRecord> UpsertPreferenceAsync(string userId, string category, bool inAppEnabled, bool emailEnabled, DateTimeOffset nowUtc, CancellationToken ct = default);

    Task CreateEmailDeliveryRequestAsync(Guid notificationId, string recipientUserId, string? recipientEmail, string status, DateTimeOffset nowUtc, CancellationToken ct = default);
    Task UpdateEmailDeliveryStatusAsync(Guid notificationId, string status, string? failureReason, DateTimeOffset nowUtc, CancellationToken ct = default);
}
