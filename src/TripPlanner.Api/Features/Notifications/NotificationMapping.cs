using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

/// <summary>Maps stored notification records to recipient-facing response contracts.</summary>
public static class NotificationMapping
{
    public static NotificationResponse ToResponse(this NotificationRecord record, bool canOpenTrip)
    {
        NotificationCompletedBy? completedBy = record.CompletedByUserId is null
            ? null
            : new NotificationCompletedBy(record.CompletedByUserId, record.CompletedByDisplayName);

        return new NotificationResponse(
            record.NotificationId,
            record.Category,
            record.Kind,
            record.TargetType,
            record.RelatedTripId,
            record.RelatedTripName,
            record.Title,
            record.Message,
            record.CreatedAtUtc,
            record.ReadAtUtc,
            record.ActionStatus,
            record.CompletedAtUtc,
            completedBy,
            record.EmailDeliveryStatus,
            canOpenTrip);
    }
}
