using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.TripMaps;

public static class TripMapEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTripMapEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Built-in trip map. Trip-scoped read; access is resolved per trip inside the handler.
        var group = endpoints.MapGroup("/api/trips/{tripId:guid}")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("TripMaps");

        group.MapGetTripMap();
        return endpoints;
    }
}
