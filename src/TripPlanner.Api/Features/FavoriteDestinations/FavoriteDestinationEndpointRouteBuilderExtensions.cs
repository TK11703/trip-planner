using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class FavoriteDestinationEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapFavoriteDestinationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/favorite-destinations")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Favorite destinations");

        group.MapGetFavoriteDestinations();
        group.MapCreateFavoriteDestination();
        group.MapUpdateFavoriteDestination();
        group.MapDeleteFavoriteDestination();
        group.MapDeleteFavoriteDestinations();
        group.MapImportFavoriteDestinations();
        return endpoints;
    }
}