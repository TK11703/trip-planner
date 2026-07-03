using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.ThemePreferences;

public static class ThemePreferenceEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapThemePreferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/theme-preference")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Theme preferences");

        group.MapGetThemePreference();
        group.MapPutThemePreference();
        return endpoints;
    }
}
