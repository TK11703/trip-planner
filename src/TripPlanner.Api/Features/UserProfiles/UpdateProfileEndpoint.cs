using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Profile;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Features.UserProfiles;

public static class UpdateProfileEndpoint
{
    public static RouteGroupBuilder MapUpdateProfile(this RouteGroupBuilder group)
    {
        group.MapPut("/", HandleAsync).WithName("UpdateUserProfile");
        return group;
    }

    private static async Task<Results<Ok<UserProfileResponse>, BadRequest<ApiError>, NotFound>> HandleAsync(
        UpdateUserProfileRequest request,
        UserProfileValidator validator,
        ICurrentUser currentUser,
        IUserProfileRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return TypedResults.BadRequest(validation.Error!);
        }

        await repository.EnsureFromAuthenticatedUserAsync(
            currentUser.UserId,
            currentUser.FirstName,
            currentUser.LastName,
            currentUser.DisplayName,
            currentUser.Email,
            clock.UtcNow,
            cancellationToken);

        var profile = await repository.UpdateAsync(currentUser.UserId, request, clock.UtcNow, cancellationToken);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
    }
}
