using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Database.Audit;
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

        var affected = await sharing.DeleteShareAsync(access.OwnerUserId, tripId, userId, ct);
        if (affected == 0)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-share", $"{tripId}:{userId}", AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        await audit.RecordAsync(callerId, AuditOperations.TripShareDelete, "trip-share", $"{tripId}:{userId}", AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.NoContent();
    }
}
