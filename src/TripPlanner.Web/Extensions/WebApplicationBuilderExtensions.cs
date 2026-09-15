using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Features.Theme;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Maps;
using TripPlanner.Web.Features.Notifications;
using TripPlanner.Web.Features.EmailIngestion;
using TripPlanner.Web.Features.InboxHistory;
using TripPlanner.Web.Health;

namespace TripPlanner.Web.Extensions;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder AddTripPlannerWeb(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults();
        builder.AddTripPlannerAuthentication();
        builder.AddTripPlannerDataProtection();

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<ThemeStateService>();
        builder.Services.AddScoped<AccountThemeInitializer>();
        builder.Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        builder.Services.AddSingleton<IMotivationalFactRotation, MotivationalFactRotation>();
        builder.Services.AddScoped<ITripPlannerApiTokenProvider, MicrosoftIdentityTripPlannerApiTokenProvider>();
        builder.Services.AddTransient<AuthenticatedApiTokenHandler>();
        builder.Services.AddScoped<IMapPreferenceProvider, MapPreferenceProvider>();

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

        builder.Services.AddHttpClient<INotificationApiClient, NotificationApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://api");
        })
        .AddHttpMessageHandler<AuthenticatedApiTokenHandler>();

        builder.Services.AddHttpClient<IEmailIngestionApiClient, EmailIngestionApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://api");
        })
        .AddHttpMessageHandler<AuthenticatedApiTokenHandler>();

        builder.Services.AddHttpClient<IInboxHistoryApiClient, InboxHistoryApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://api");
        })
        .AddHttpMessageHandler<AuthenticatedApiTokenHandler>();

        // Unauthenticated probe client: no token handler, because /alive is anonymous.
        builder.Services.AddHttpClient(ApiReachabilityHealthCheck.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https+http://api");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // Readiness checks. None are tagged "live", so /alive stays dependency-free.
        builder.Services.AddHealthChecks()
            .AddCheck<ApiReachabilityHealthCheck>(ApiReachabilityHealthCheck.Name, tags: ["ready"])
            .AddCheck<AuthenticationConfigurationHealthCheck>(AuthenticationConfigurationHealthCheck.Name, tags: ["ready"])
            .AddCheck<DataProtectionHealthCheck>(DataProtectionHealthCheck.Name, tags: ["ready"]);

        return builder;
    }
}
