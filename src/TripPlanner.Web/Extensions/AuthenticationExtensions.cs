using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using System.Security.Claims;
using System.Security.Cryptography;
using TripPlanner.Web.Features.TripDataChat;
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

        builder.Services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new OpenIdConnectEvents();
            var existingTokenValidated = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                if (existingTokenValidated is not null)
                {
                    await existingTokenValidated(context);
                }

                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    var existingEpoch = identity.FindFirst(TripChatSessionEpoch.ClaimType);
                    if (existingEpoch is not null)
                    {
                        identity.RemoveClaim(existingEpoch);
                    }

                    identity.AddClaim(new Claim(
                        TripChatSessionEpoch.ClaimType,
                        Convert.ToHexString(RandomNumberGenerator.GetBytes(24))));
                }
            };
        });

        // The token cache is per-process, so a deployment, a restart, or a scale-to-zero leaves a
        // still-valid auth cookie with no access token behind it. Re-acquiring needs a redirect.
        builder.Services.AddMicrosoftIdentityConsentHandler();
        builder.Services.AddScoped<IApiChallengeHandler, MicrosoftIdentityApiChallengeHandler>();

        builder.Services.AddControllersWithViews()
            .AddMicrosoftIdentityUI();

        return builder;
    }
}
