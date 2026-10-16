using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripMaps;

public static class GetTripMapEndpoint
{
    public static RouteGroupBuilder MapGetTripMap(this RouteGroupBuilder group)
    {
        group.MapGet("/map", HandleAsync).WithName("GetTripMap");
        return group;
    }

    private static async Task<Results<Ok<TripMapResponse>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripItemRepository items,
        TripMapLocationResolver locationResolver,
        IAuditRepository audit,
        IClock clock,
        CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-map", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var trackedItems = await items.GetTrackedItemsAsync(access.OwnerUserId, tripId, ct);

        return TypedResults.Ok(await locationResolver.ResolveAsync(trackedItems, ct));
    }
}
