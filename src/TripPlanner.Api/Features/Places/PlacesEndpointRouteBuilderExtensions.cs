using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.Places;

public static class PlacesEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPlaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Address/place typeahead. Requires an authenticated user; no trip scope is involved.
        var group = endpoints.MapGroup("/api/places")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Places");

        group.MapSuggestPlaces();
        return endpoints;
    }
}
