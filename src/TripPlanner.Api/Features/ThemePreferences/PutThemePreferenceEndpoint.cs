using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Theme;
using TripPlanner.Database.ThemePreferences;

namespace TripPlanner.Api.Features.ThemePreferences;

public static class PutThemePreferenceEndpoint
{
    public static RouteGroupBuilder MapPutThemePreference(this RouteGroupBuilder group)
    {
        group.MapPut("/", HandleAsync).WithName("PutThemePreference");
        return group;
    }

    private static async Task<Results<Ok<ThemePreferenceResponse>, BadRequest>> HandleAsync(
        UpdateThemePreferenceRequest request,
        ThemePreferenceValidator validator,
        ICurrentUser currentUser,
        IThemePreferenceRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!validator.IsValid(request))
        {
            return TypedResults.BadRequest();
        }

        var record = await repository.UpsertAsync(currentUser.UserId, request.ThemeMode.Value, clock.UtcNow, cancellationToken);
        return TypedResults.Ok(new ThemePreferenceResponse(record.ThemeMode, ThemePreferenceSource.AccountPreference, record.UpdatedAtUtc));
    }
}
