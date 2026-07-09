using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

/// <summary>
/// Creates notifications honoring per-recipient category/channel preferences and requests email
/// delivery through the outbox. In-app creation never fails because of email problems.
/// </summary>
public interface INotificationService
{
    Task<NotificationRecord?> CreateAsync(NewNotification notification, CancellationToken ct);
}

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly INotificationEmailSender _emailSender;

    public NotificationService(INotificationRepository repository, INotificationEmailSender emailSender)
    {
        _repository = repository;
        _emailSender = emailSender;
    }

    public async Task<NotificationRecord?> CreateAsync(NewNotification notification, CancellationToken ct)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var decision = await ResolveDeliveryAsync(notification.RecipientUserId, notification.Category, ct);

        // A recipient's saved preferences overrule delivery. When every channel is disabled for the
        // category, nothing is delivered on any channel.
        if (decision.Outcome == NotificationDeliveryOutcome.SuppressAll)
        {
            return null;
        }

        // In-app is the master channel for a notification's existence. If in-app is disabled we do not
        // persist an in-app notification; email-only delivery is out of scope for these categories.
        if (!decision.InAppAllowed)
        {
            return null;
        }

        var record = await _repository.CreateAsync(notification, nowUtc, ct);
        if (record is null)
        {
            // Duplicate for (recipient, source event) — suppress silently.
            return null;
        }

        if (decision.EmailAllowed)
        {
            await DeliverEmailAsync(record, notification.RecipientEmail, nowUtc, ct);
        }

        return record;
    }

    private async Task<NotificationDeliveryDecision> ResolveDeliveryAsync(string userId, string category, CancellationToken ct)
    {
        var preferences = await _repository.GetPreferencesAsync(userId, ct);
        var match = preferences.FirstOrDefault(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase));
        return match is not null
            ? NotificationDeliveryDecision.FromPreference(match.InAppEnabled, match.EmailEnabled)
            : NotificationDeliveryDecision.FromDefaults(category);
    }

    private async Task DeliverEmailAsync(NotificationRecord record, string? recipientEmail, DateTimeOffset nowUtc, CancellationToken ct)
    {
        var initialStatus = string.IsNullOrWhiteSpace(recipientEmail) ? "suppressed" : "pending";
        await _repository.CreateEmailDeliveryRequestAsync(record.NotificationId, record.RecipientUserId, recipientEmail, initialStatus, nowUtc, ct);

        if (initialStatus == "suppressed")
        {
            return;
        }

        EmailSendResult result;
        try
        {
            var email = new NotificationEmail(record.NotificationId, record.RecipientUserId, recipientEmail, record.Title, record.Message);
            result = await _emailSender.SendAsync(email, ct);
        }
        catch (Exception ex)
        {
            result = EmailSendResult.Failure(ex.Message);
        }

        var status = result switch
        {
            { Sent: true } => "sent",
            { Suppressed: true } => "suppressed",
            _ => "failed"
        };

        await _repository.UpdateEmailDeliveryStatusAsync(record.NotificationId, status, result.FailureReason, DateTimeOffset.UtcNow, ct);
    }
}
