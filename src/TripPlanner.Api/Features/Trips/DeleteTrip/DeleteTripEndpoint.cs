using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.Trips.DeleteTrip;

public static class DeleteTripEndpoint
{
    public static RouteGroupBuilder MapDeleteTrip(this RouteGroupBuilder group)
    {
        group.MapDelete("/{tripId:guid}", HandleAsync).WithName("DeleteTrip");
        return group;
    }

    private static async Task<Results<NoContent, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripCommandRepository commands,
        IAuditRepository audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var callerId = currentUser.UserId;
        // Deleting a trip is owner-only; collaborators and viewers cannot delete the trip.
        var access = await accessResolver.ResolveAsync(callerId, tripId, cancellationToken);
        if (access is null || !access.IsOwner())
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var affected = await commands.DeleteAsync(access.OwnerUserId, tripId, cancellationToken);
        if (affected == 0)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        await audit.RecordAsync(callerId, AuditOperations.TripDelete, "trip", tripId.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        return TypedResults.NoContent();
    }
}
