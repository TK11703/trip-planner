using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Tests.Notifications;

/// <summary>A minimal in-memory notification repository for exercising delivery preference logic.</summary>
internal sealed class FakeNotificationRepository : INotificationRepository
{
    private readonly Dictionary<(string User, string Category), NotificationPreferenceRecord> _preferences = new();

    public List<NewNotification> Created { get; } = new();
    public List<(Guid NotificationId, string Status)> EmailRequests { get; } = new();

    public void SetPreference(string userId, string category, bool inAppEnabled, bool emailEnabled)
        => _preferences[(userId, category)] = new NotificationPreferenceRecord(userId, category, inAppEnabled, emailEnabled, DateTimeOffset.UtcNow);

    public Task<int> GetUnreadCountAsync(string recipientUserId, CancellationToken ct = default) => Task.FromResult(0);

    public Task<IReadOnlyList<NotificationRecord>> GetListAsync(string recipientUserId, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<NotificationRecord>>(Array.Empty<NotificationRecord>());

    public Task<NotificationRecord?> GetAsync(string recipientUserId, Guid notificationId, CancellationToken ct = default)
        => Task.FromResult<NotificationRecord?>(null);

    public Task<NotificationRecord?> CreateAsync(NewNotification notification, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Created.Add(notification);
        var record = new NotificationRecord(
            Guid.NewGuid(),
            notification.RecipientUserId,
            notification.Category,
            notification.Kind,
            notification.TargetType,
            notification.RelatedTripId,
            RelatedTripName: null,
            notification.Title,
            notification.Message,
            nowUtc,
            ReadAtUtc: null,
            NotificationActionStatus.NotApplicable,
            CompletedAtUtc: null,
            CompletedByUserId: null,
            CompletedByDisplayName: null,
            NotificationEmailStatus.NotRequested);
        return Task.FromResult<NotificationRecord?>(record);
    }

    public Task<DateTimeOffset?> MarkReadAsync(string recipientUserId, Guid notificationId, DateTimeOffset nowUtc, CancellationToken ct = default)
        => Task.FromResult<DateTimeOffset?>(null);

    public Task<int> MarkAllReadAsync(string recipientUserId, DateTimeOffset nowUtc, CancellationToken ct = default) => Task.FromResult(0);

    public Task<bool> DeleteAsync(string recipientUserId, Guid notificationId, DateTimeOffset nowUtc, CancellationToken ct = default) => Task.FromResult(false);

    public Task<NotificationRecord?> CompleteAsync(string recipientUserId, Guid notificationId, string completedByUserId, string? completedByDisplayName, DateTimeOffset nowUtc, CancellationToken ct = default)
        => Task.FromResult<NotificationRecord?>(null);

    public Task<IReadOnlyList<NotificationPreferenceRecord>> GetPreferencesAsync(string userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<NotificationPreferenceRecord>>(
            _preferences.Where(p => string.Equals(p.Key.User, userId, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).ToArray());

    public Task<NotificationPreferenceRecord> UpsertPreferenceAsync(string userId, string category, bool inAppEnabled, bool emailEnabled, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        var record = new NotificationPreferenceRecord(userId, category, inAppEnabled, emailEnabled, nowUtc);
        _preferences[(userId, category)] = record;
        return Task.FromResult(record);
    }

    public Task CreateEmailDeliveryRequestAsync(Guid notificationId, string recipientUserId, string? recipientEmail, string status, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        EmailRequests.Add((notificationId, status));
        return Task.CompletedTask;
    }

    public Task UpdateEmailDeliveryStatusAsync(Guid notificationId, string status, string? failureReason, DateTimeOffset nowUtc, CancellationToken ct = default)
        => Task.CompletedTask;
}

/// <summary>Records emails that were actually attempted (i.e. a recipient email was present).</summary>
internal sealed class FakeEmailSender : INotificationEmailSender
{
    public List<NotificationEmail> Sent { get; } = new();

    public Task<EmailSendResult> SendAsync(NotificationEmail email, CancellationToken ct)
    {
        Sent.Add(email);
        return Task.FromResult(EmailSendResult.Success());
    }
}
