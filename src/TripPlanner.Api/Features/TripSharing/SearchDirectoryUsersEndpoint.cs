using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripSharing;

public static class SearchDirectoryUsersEndpoint
{
    public static RouteGroupBuilder MapSearchDirectoryUsers(this RouteGroupBuilder group)
    {
        group.MapGet("/directory-users", HandleAsync).WithName("SearchTripShareDirectoryUsers");
        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<DirectoryUserResult>>, BadRequest<ApiError>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        string? query,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripSharingRepository sharing,
        IUserDirectoryLookup directory,
        TripSharingValidator validator,
        IAuditRepository audit,
        IClock clock,
        CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null || !access.IsOwner())
        {
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var validation = validator.ValidateSearchQuery(query);
        if (!validation.IsValid)
        {
            return TypedResults.BadRequest(validation.Error!);
        }

        var results = await directory.SearchAsync(query!, ct);

        // Never offer the owner or people who already have access as new-share choices.
        var members = await sharing.GetSharesAsync(tripId, ct);
        var excluded = members.Select(m => m.UserId).Append(access.OwnerUserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filtered = results.Where(r => !excluded.Contains(r.UserId)).ToArray();

        await audit.RecordAsync(callerId, AuditOperations.DirectorySearch, "trip-share", tripId.ToString(), AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.Ok<IReadOnlyList<DirectoryUserResult>>(filtered);
    }
}
