using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Trips;
using TripPlanner.Contracts.Errors;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripSharing;

public static class GetTripSharesEndpoint
{
    public static RouteGroupBuilder MapGetTripShares(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync).WithName("GetTripShares");
        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<TripShareMember>>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripSharingRepository sharing,
        CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null || !access.IsOwner())
        {
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var members = await sharing.GetSharesAsync(tripId, ct);
        return TypedResults.Ok(members);
    }
}
