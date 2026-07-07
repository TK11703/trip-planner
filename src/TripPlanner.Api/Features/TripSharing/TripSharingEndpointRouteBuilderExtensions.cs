using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.TripSharing;

public static class TripSharingEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTripSharingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // All share endpoints are trip-scoped and require an authenticated user. Owner-only
        // enforcement is applied inside each handler through ITripAccessResolver, so collaborators
        // and viewers cannot manage sharing even though they can read the trip.
        var group = endpoints.MapGroup("/api/trips/{tripId:guid}/shares")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("TripSharing");

        group.MapGetTripShares();
        group.MapSearchDirectoryUsers();
        group.MapUpsertTripShare();
        group.MapUpdateTripShareAccess();
        group.MapDeleteTripShare();
        return endpoints;
    }
}
