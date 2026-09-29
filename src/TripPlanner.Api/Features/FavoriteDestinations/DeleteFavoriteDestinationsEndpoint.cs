using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class DeleteFavoriteDestinationsEndpoint
{
    public const int MaximumIds = 1000;

    public static RouteGroupBuilder MapDeleteFavoriteDestinations(this RouteGroupBuilder group)
    {
        group.MapPost("/delete", HandleAsync).WithName("DeleteFavoriteDestinations");
        return group;
    }

    // IDs the caller does not own are skipped, so DeletedCount can be lower than the number requested.
    private static async Task<Results<Ok<DeleteFavoriteDestinationsResponse>, UnprocessableEntity<ApiError>>> HandleAsync(
        DeleteFavoriteDestinationsRequest request,
        ICurrentUser currentUser,
        IFavoriteDestinationRepository repository,
        CancellationToken cancellationToken)
    {
        var ids = request.FavoriteDestinationIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        if (ids.Length == 0 || ids.Length > MaximumIds)
        {
            return TypedResults.UnprocessableEntity(ApiError.ValidationFailed(
                $"Select between 1 and {MaximumIds} favorites to delete.", "favoriteDestinationIds"));
        }

        var deleted = await repository.DeleteManyAsync(currentUser.UserId, ids, cancellationToken);
        return TypedResults.Ok(new DeleteFavoriteDestinationsResponse(deleted));
    }
}
