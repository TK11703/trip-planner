using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;
using TripPlanner.Api.Features.Trips.CreateTrip;
using TripPlanner.Api.Features.Trips.GetRecentTrips;
using TripPlanner.Api.Features.Trips.GetTripDetail;
using TripPlanner.Api.Features.Trips.GetTrips;
using TripPlanner.Api.Features.Trips.UpdateTrip;

namespace TripPlanner.Api.Features.Trips;

public static class TripEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapTripEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/trips")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Trips");

        group.MapGetTrips();
        group.MapGetRecentTrips();
        group.MapGetTripDetail();
        group.MapCreateTrip();
        group.MapUpdateTrip();
        return endpoints;
    }
}
