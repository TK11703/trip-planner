using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

namespace TripPlanner.Web.Extensions;

public static class AuthenticationExtensions
{
    public static WebApplicationBuilder AddTripPlannerAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(builder.Configuration, configSectionName: "AzureEntra");

        builder.Services.AddControllersWithViews()
            .AddMicrosoftIdentityUI();

        return builder;
    }
}
