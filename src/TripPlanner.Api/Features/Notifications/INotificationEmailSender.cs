namespace TripPlanner.Api.Features.Notifications;

/// <summary>An email derived from a notification, addressed to the recipient.</summary>
public sealed record NotificationEmail(
    Guid NotificationId,
    string RecipientUserId,
    string? RecipientEmail,
    string Subject,
    string Body);

/// <summary>The outcome of attempting to send a notification email.</summary>
public sealed record EmailSendResult(bool Sent, bool Suppressed, string? FailureReason)
{
    public static EmailSendResult Success() => new(true, false, null);
    public static EmailSendResult SuppressedResult(string reason) => new(false, true, reason);
    public static EmailSendResult Failure(string reason) => new(false, false, reason);
}

/// <summary>
/// Abstraction over the email transport used for notifications. Implementations must never throw
/// for a missing/invalid address; they should return a suppressed or failed result so the in-app
/// notification is preserved.
/// </summary>
public interface INotificationEmailSender
{
    Task<EmailSendResult> SendAsync(NotificationEmail email, CancellationToken ct);
}
