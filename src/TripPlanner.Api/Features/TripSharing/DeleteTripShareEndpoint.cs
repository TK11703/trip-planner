using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripSharing;

public static class DeleteTripShareEndpoint
{
    public static RouteGroupBuilder MapDeleteTripShare(this RouteGroupBuilder group)
    {
        group.MapDelete("/{userId}", HandleAsync).WithName("DeleteTripShare");
        return group;
    }

    private static async Task<Results<NoContent, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        string userId,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripSharingRepository sharing,
        IAuditRepository audit,
        INotificationService notifications,
        IClock clock,
        CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null || !access.IsOwner())
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-share", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        // Capture the member's contact details before removing the share so we can still email them.
        var member = (await sharing.GetSharesAsync(tripId, ct))
            .FirstOrDefault(m => string.Equals(m.UserId, userId, StringComparison.OrdinalIgnoreCase));

        var affected = await sharing.DeleteShareAsync(access.OwnerUserId, tripId, userId, ct);
        if (affected == 0)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-share", $"{tripId}:{userId}", AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        await audit.RecordAsync(callerId, AuditOperations.TripShareDelete, "trip-share", $"{tripId}:{userId}", AuditResults.Success, clock.UtcNow, ct);

        // Alert the person that their access was removed, specifying who removed it. This is
        // person-targeted (no trip link, since they can no longer open the trip) and awareness-only.
        // Notification failure must not fail the removal.
        try
        {
            var actorName = currentUser.DisplayName ?? "The trip owner";
            await notifications.CreateAsync(new NewNotification(
                RecipientUserId: userId,
                Category: NotificationCategories.TripSharing,
                Kind: NotificationKind.Awareness,
                TargetType: NotificationTargetType.Person,
                RelatedTripId: null,
                Title: "Your trip access was removed",
                Message: $"{actorName} removed your access to a shared trip.",
                SourceEventKey: $"trip-share-removed:{tripId}:{userId}:{Guid.NewGuid():N}",
                RecipientEmail: member?.Email), ct);
        }
        catch
        {
            // Swallow notification failures; the removal succeeded and is the primary outcome.
        }

        return TypedResults.NoContent();
    }
}
