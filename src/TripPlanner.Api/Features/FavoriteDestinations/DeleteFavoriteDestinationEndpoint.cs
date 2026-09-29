using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Errors;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class DeleteFavoriteDestinationEndpoint
{
    public static RouteGroupBuilder MapDeleteFavoriteDestination(this RouteGroupBuilder group)
    {
        group.MapDelete("/{favoriteDestinationId:guid}", HandleAsync).WithName("DeleteFavoriteDestination");
        return group;
    }

    private static async Task<Results<NoContent, NotFound<ApiError>>> HandleAsync(
        Guid favoriteDestinationId,
        ICurrentUser currentUser,
        IFavoriteDestinationRepository repository,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(currentUser.UserId, favoriteDestinationId, cancellationToken);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound(ApiError.NotFoundOrDenied());
    }
}