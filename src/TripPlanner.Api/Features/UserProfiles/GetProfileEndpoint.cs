using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Profile;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Features.UserProfiles;

public static class GetProfileEndpoint
{
    public static RouteGroupBuilder MapGetProfile(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync).WithName("GetUserProfile");
        return group;
    }

    private static async Task<Ok<UserProfileResponse>> HandleAsync(
        ICurrentUser currentUser,
        IUserProfileRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var profile = await repository.EnsureFromAuthenticatedUserAsync(
            currentUser.UserId,
            currentUser.FirstName,
            currentUser.LastName,
            currentUser.DisplayName,
            currentUser.Email,
            clock.UtcNow,
            cancellationToken);

        return TypedResults.Ok(profile);
    }
}
