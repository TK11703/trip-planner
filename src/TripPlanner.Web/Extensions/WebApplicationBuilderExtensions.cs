using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Features.Theme;

namespace TripPlanner.Web.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddTripPlannerWeb(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults();
        builder.AddTripPlannerAuthentication();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ThemeStateService>();
        builder.Services.AddScoped<AccountThemeInitializer>();
        builder.Services.AddScoped<ITripPlannerApiTokenProvider, MicrosoftIdentityTripPlannerApiTokenProvider>();
        builder.Services.AddTransient<AuthenticatedApiTokenHandler>();

        // HTTP client to the authenticated Minimal API resolved via Aspire service discovery.
        builder.Services.AddHttpClient<ITripApiClient, TripApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://api");
        })
        .AddHttpMessageHandler<AuthenticatedApiTokenHandler>();

        builder.Services.AddHttpClient<IThemePreferenceApiClient, ThemePreferenceApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://api");
        })
        .AddHttpMessageHandler<AuthenticatedApiTokenHandler>();

        builder.Services.AddHttpClient<IProfileApiClient, ProfileApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://api");
        })
        .AddHttpMessageHandler<AuthenticatedApiTokenHandler>();

        return builder;
    }
}
