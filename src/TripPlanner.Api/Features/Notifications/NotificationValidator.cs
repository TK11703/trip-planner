using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

/// <summary>Validates actionable-notification completion rules.</summary>
public sealed class NotificationValidator
{
    /// <summary>
    /// Returns an error when the notification cannot be completed: awareness notifications are not
    /// actionable, and already-completed notifications cannot be completed again.
    /// </summary>
    public ApiError? ValidateCompletable(NotificationRecord record)
    {
        if (record.Kind != NotificationKind.Actionable)
        {
            return new ApiError("conflict", "This notification is informational and cannot be completed.");
        }

        if (record.ActionStatus == NotificationActionStatus.Completed)
        {
            return new ApiError("conflict", "This notification has already been completed.");
        }

        return null;
    }
}
