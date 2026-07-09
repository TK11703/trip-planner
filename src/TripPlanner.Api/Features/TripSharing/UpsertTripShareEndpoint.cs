using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripSharing;

public static class UpsertTripShareEndpoint
{
    public static RouteGroupBuilder MapUpsertTripShare(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync).WithName("UpsertTripShare");
        return group;
    }

    private static async Task<Results<Ok<TripShareMember>, BadRequest<ApiError>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        UpsertTripShareRequest request,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripSharingRepository sharing,
        TripSharingValidator validator,
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

        var validation = validator.ValidateUpsert(request);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(callerId, AuditOperations.TripShareCreate, "trip-share", tripId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(validation.Error!);
        }

        if (string.Equals(request.UserId, access.OwnerUserId, StringComparison.OrdinalIgnoreCase))
        {
            await audit.RecordAsync(callerId, AuditOperations.TripShareCreate, "trip-share", tripId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(ApiError.ValidationFailed("You already have full access to this trip as its owner.", "userId"));
        }

        var member = await sharing.UpsertShareAsync(access.OwnerUserId, tripId, request, clock.UtcNow, ct);
        if (member is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-share", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        await audit.RecordAsync(callerId, AuditOperations.TripShareCreate, "trip-share", $"{tripId}:{member.UserId}", AuditResults.Success, clock.UtcNow, ct);

        // Alert the member that a trip was shared with them. This is awareness-only (no action required)
        // and links to the trip. Failure to notify must not fail the share itself.
        try
        {
            var sharerName = currentUser.DisplayName ?? "Someone";
            await notifications.CreateAsync(new NewNotification(
                RecipientUserId: member.UserId,
                Category: NotificationCategories.TripSharing,
                Kind: NotificationKind.Awareness,
                TargetType: NotificationTargetType.Trip,
                RelatedTripId: tripId,
                Title: "A trip was shared with you",
                Message: $"{sharerName} shared a trip with you as a {AccessLevelLabel(member.AccessLevel)}.",
                SourceEventKey: TripSharingNotificationKeys.Added(tripId, member.UserId),
                RecipientEmail: member.Email), ct);
        }
        catch
        {
            // Swallow notification failures; the share succeeded and is the primary outcome.
        }

        return TypedResults.Ok(member);
    }

    internal static string AccessLevelLabel(TripAccessLevel level)
        => level == TripAccessLevel.Collaborator ? "collaborator" : "viewer";
}
