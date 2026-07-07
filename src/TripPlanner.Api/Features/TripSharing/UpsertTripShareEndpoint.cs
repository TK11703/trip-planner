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
        return TypedResults.Ok(member);
    }
}
