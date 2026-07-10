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
using TripPlanner.Database.TripSharing;

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
        ITripAccessResolver accessResolver,
        ITripReadRepository tripReads,
        ITripItemRepository itemReads,
        ITripSharingRepository sharing,
        IAuditRepository audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, cancellationToken);
        if (access is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        // Reads use the trip owner's id so existing owner-scoped queries return the trip's data for
        // owners, collaborators, and viewers alike.
        var ownerId = access.OwnerUserId;
        var detail = await tripReads.GetDetailAsync(ownerId, tripId, cancellationToken);
        if (detail is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var legs = await itemReads.GetLegsAsync(ownerId, tripId, cancellationToken);
        var items = await itemReads.GetTrackedItemsAsync(ownerId, tripId, cancellationToken);
        var sharedPeople = await sharing.GetSharesAsync(tripId, cancellationToken);
        // Trip estimated total = sum of leg-assigned item estimated costs (matches the sum of the
        // per-leg estimated totals shown on the timeline). Items with no estimate are excluded.
        var estimatedCostTotal = items
            .Where(i => i.TripLegId is not null && i.EstimatedCost is not null)
            .Sum(i => i.EstimatedCost!.Value);
        var withChildren = detail with
        {
            Legs = legs,
            TrackedItems = items,
            AccessLevel = access.AccessLevel,
            IsOwner = access.IsOwner(),
            SharedPeople = sharedPeople,
            EstimatedCostTotal = estimatedCostTotal
        };
        await audit.RecordAsync(callerId, AuditOperations.TripRead, "trip", tripId.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        return TypedResults.Ok(withChildren);
    }
}
