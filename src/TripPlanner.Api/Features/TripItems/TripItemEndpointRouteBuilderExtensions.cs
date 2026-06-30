using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;
using TripPlanner.Api.Features.Timeline;

namespace TripPlanner.Api.Features.TripItems;

public static class TripItemEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTripItemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tripScoped = endpoints.MapGroup("/api/trips/{tripId:guid}")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("TripItems");

        tripScoped.MapGroup("/legs").MapTripLegs();
        tripScoped.MapGroup("/items").MapTrackedItems();
        tripScoped.MapGetTripTimeline();
        return endpoints;
    }
}
