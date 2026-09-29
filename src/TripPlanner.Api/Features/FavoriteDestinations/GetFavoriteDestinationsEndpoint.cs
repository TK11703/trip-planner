using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public static class GetFavoriteDestinationsEndpoint
{
    public static RouteGroupBuilder MapGetFavoriteDestinations(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync).WithName("GetFavoriteDestinations");
        return group;
    }

    private static async Task<Ok<IReadOnlyList<FavoriteDestinationDto>>> HandleAsync(
        string? q,
        ICurrentUser currentUser,
        IFavoriteDestinationRepository repository,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await repository.GetAllAsync(currentUser.UserId, q, cancellationToken));
}