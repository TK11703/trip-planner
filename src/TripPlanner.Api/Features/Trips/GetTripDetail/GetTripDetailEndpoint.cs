using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Trips;
using TripPlanner.Database.TripItems;

namespace TripPlanner.Api.Features.Trips.GetTripDetail;

public static class GetTripDetailEndpoint
{
    public static RouteGroupBuilder MapGetTripDetail(this RouteGroupBuilder group)
    {
        group.MapGet("/{tripId:guid}", HandleAsync).WithName("GetTripDetail");
        return group;
    }

    private static async Task<Results<Ok<TripDetail>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        ICurrentUser currentUser,
        ITripReadRepository tripReads,
        ITripItemRepository itemReads,
        IAuditRepository audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        var detail = await tripReads.GetDetailAsync(ownerId, tripId, cancellationToken);
        if (detail is null)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "trip", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var legs = await itemReads.GetLegsAsync(ownerId, tripId, cancellationToken);
        var items = await itemReads.GetTrackedItemsAsync(ownerId, tripId, cancellationToken);
        var withChildren = detail with { Legs = legs, TrackedItems = items };
        await audit.RecordAsync(ownerId, AuditOperations.TripRead, "trip", tripId.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        return TypedResults.Ok(withChildren);
    }
}
