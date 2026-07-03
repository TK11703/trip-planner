using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Theme;
using TripPlanner.Database.ThemePreferences;

namespace TripPlanner.Api.Features.ThemePreferences;

public static class GetThemePreferenceEndpoint
{
    public static RouteGroupBuilder MapGetThemePreference(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync).WithName("GetThemePreference");
        return group;
    }

    private static async Task<Results<Ok<ThemePreferenceResponse>, NoContent>> HandleAsync(
        ICurrentUser currentUser,
        IThemePreferenceRepository repository,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetAsync(currentUser.UserId, cancellationToken);
        if (record is null)
        {
            return TypedResults.NoContent();
        }

        return TypedResults.Ok(new ThemePreferenceResponse(record.ThemeMode, ThemePreferenceSource.AccountPreference, record.UpdatedAtUtc));
    }
}
