using Microsoft.Extensions.Logging;

namespace TripPlanner.Api.Features.Notifications;

/// <summary>
/// Development/default email sender. It does not contact a real transport; it logs the message and
/// reports success when an address is present, or suppression when it is missing. This keeps in-app
/// delivery reliable and lets a real transport be substituted via configuration later.
/// </summary>
public sealed partial class DevelopmentNotificationEmailSender : INotificationEmailSender
{
    private readonly ILogger<DevelopmentNotificationEmailSender> _logger;

    public DevelopmentNotificationEmailSender(ILogger<DevelopmentNotificationEmailSender> logger) => _logger = logger;

    public Task<EmailSendResult> SendAsync(NotificationEmail email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email.RecipientEmail))
        {
            LogSuppressed(email.NotificationId);
            return Task.FromResult(EmailSendResult.SuppressedResult("No recipient email address."));
        }

        LogSent(email.NotificationId, email.RecipientEmail, email.Subject);
        return Task.FromResult(EmailSendResult.Success());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Suppressing notification email {NotificationId}: no recipient address.")]
    private partial void LogSuppressed(Guid notificationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification email {NotificationId} to {Recipient}: {Subject}")]
    private partial void LogSent(Guid notificationId, string recipient, string subject);
}
