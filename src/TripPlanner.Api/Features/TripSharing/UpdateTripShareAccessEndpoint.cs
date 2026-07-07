using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripSharing;

public static class UpdateTripShareAccessEndpoint
{
    public static RouteGroupBuilder MapUpdateTripShareAccess(this RouteGroupBuilder group)
    {
        group.MapPut("/{userId}", HandleAsync).WithName("UpdateTripShareAccess");
        return group;
    }

    private static async Task<Results<Ok<TripShareMember>, BadRequest<ApiError>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        string userId,
        UpdateTripShareAccessRequest request,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripSharingRepository sharing,
        TripSharingValidator validator,
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

        var validation = validator.ValidateAccessChange(request);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(callerId, AuditOperations.TripShareUpdate, "trip-share", $"{tripId}:{userId}", AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(validation.Error!);
        }

        var member = await sharing.UpdateAccessAsync(access.OwnerUserId, tripId, userId, request.AccessLevel, clock.UtcNow, ct);
        if (member is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-share", $"{tripId}:{userId}", AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        await audit.RecordAsync(callerId, AuditOperations.TripShareUpdate, "trip-share", $"{tripId}:{userId}", AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.Ok(member);
    }
}
