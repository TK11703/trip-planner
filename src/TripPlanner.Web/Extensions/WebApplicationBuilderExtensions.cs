using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using TripPlanner.Web.Features.Trips;

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

        // HTTP client to the authenticated Minimal API resolved via Aspire service discovery.
        builder.Services.AddHttpClient<ITripApiClient, TripApiClient>(client =>
        {
            client.BaseAddress = new Uri("https+http://api");
        });

        return builder;
    }
}
