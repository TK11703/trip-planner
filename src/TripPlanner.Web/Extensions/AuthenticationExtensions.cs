using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TripPlanner.Web.Features.Authentication;

namespace TripPlanner.Web.Extensions;

public static class AuthenticationExtensions
{
    public static WebApplicationBuilder AddTripPlannerAuthentication(this WebApplicationBuilder builder)
    {
        var apiScopes = builder.Configuration
            .GetSection("AzureEntra:ApiScopes")
            .Get<string[]>()
            ?? [];

        builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(builder.Configuration, configSectionName: "AzureEntra")
            .EnableTokenAcquisitionToCallDownstreamApi(apiScopes)
            .AddInMemoryTokenCaches();

        // The token cache is per-process, so a deployment, a restart, or a scale-to-zero leaves a
        // still-valid auth cookie with no access token behind it. Re-acquiring needs a redirect.
        builder.Services.AddMicrosoftIdentityConsentHandler();
        builder.Services.AddScoped<IApiChallengeHandler, MicrosoftIdentityApiChallengeHandler>();

        builder.Services.AddControllersWithViews()
            .AddMicrosoftIdentityUI();

        return builder;
    }
}
