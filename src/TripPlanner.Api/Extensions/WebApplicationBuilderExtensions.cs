using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Security;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Timeline;
using TripPlanner.Database.TripSharing;
using TripPlanner.Database.Initialization;
using TripPlanner.Contracts.Common;
using TripPlanner.Api.Features.Trips.CreateTrip;
using TripPlanner.Api.Features.Trips.UpdateTrip;
using TripPlanner.Api.Features.TripItems;
using TripPlanner.Api.Features.TripSharing;
using TripPlanner.Database.ThemePreferences;
using TripPlanner.Api.Features.ThemePreferences;
using TripPlanner.Api.Features.UserProfiles;
using TripPlanner.Api.Features.Timezones;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Api.Features.Places;
using TripPlanner.Database.UserProfiles;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Extensions;

public static class WebApplicationBuilderExtensions
{
    public const string AuthenticatedUserPolicy = AuthenticationExtensions.AuthenticatedUserPolicy;

    public static WebApplicationBuilder AddTripPlannerApi(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults();
        builder.AddTripPlannerAuthentication();

        builder.Services.AddOpenApi();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddSingleton<IClock, SystemClock>();

        builder.Services.AddSingleton<ISqlFileProvider>(_ => new SqlFileProvider());
        builder.Services.AddScoped<IPostgresConnectionFactory, PostgresConnectionFactory>();
        builder.Services.AddScoped<ITripReadRepository, TripReadRepository>();
        builder.Services.AddScoped<ITripCommandRepository, TripCommandRepository>();
        builder.Services.AddScoped<ITripItemRepository, TripItemRepository>();
        builder.Services.AddScoped<ITimelineRepository, TimelineRepository>();
        builder.Services.AddScoped<ITripSharingRepository, TripSharingRepository>();
        builder.Services.AddScoped<ITripAccessResolver, TripAccessResolver>();
        builder.Services.AddScoped<IAuditRepository, AuditRepository>();
        builder.Services.AddScoped<IThemePreferenceRepository, ThemePreferenceRepository>();
        builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<IItineraryNotificationService, ItineraryNotificationService>();
        builder.Services.AddSingleton<INotificationEmailSender, DevelopmentNotificationEmailSender>();

        builder.Services.AddSingleton<CreateTripValidator>();
        builder.Services.AddSingleton<UpdateTripValidator>();
        builder.Services.AddSingleton<TripLegValidator>();
        builder.Services.AddSingleton<TrackedItemValidator>();
        builder.Services.AddSingleton<TripSharingValidator>();
        builder.Services.AddSingleton<ThemePreferenceValidator>();
        builder.Services.AddSingleton<UserProfileValidator>();
        builder.Services.AddSingleton<NotificationValidator>();
        builder.Services.AddSingleton<NotificationPreferenceValidator>();
        builder.Services.AddSingleton<ITimezoneIdValidator, TimezoneIdValidator>();

        // Tenant directory lookup for the share dialog. The credential is resolved from configuration:
        // a client secret (from user-secrets/Key Vault, never hardcoded) enables app-only Graph access;
        // otherwise DefaultAzureCredential uses managed identity when hosted and developer sign-in locally.
        builder.Services.AddSingleton<TokenCredential>(_ => CreateDirectoryCredential(builder.Configuration));
        builder.Services.AddHttpClient(GraphUserDirectoryLookup.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://graph.microsoft.com/");
        });
        builder.Services.AddScoped<IUserDirectoryLookup, GraphUserDirectoryLookup>();

        // Azure Maps address/place typeahead + geocoding. The subscription key is environment-driven
        // (AzureMaps:SubscriptionKey) and both capabilities degrade gracefully when it is unset.
        builder.Services.AddHttpClient(AzureMapsPlaceSuggestionLookup.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://atlas.microsoft.com/");
        });
        builder.Services.AddScoped<AzureMapsPlaceSuggestionLookup>();
        builder.Services.AddScoped<IPlaceSuggestionLookup>(sp => sp.GetRequiredService<AzureMapsPlaceSuggestionLookup>());
        builder.Services.AddScoped<IPlaceGeocoder>(sp => sp.GetRequiredService<AzureMapsPlaceSuggestionLookup>());

        builder.Services.AddSingleton<DatabaseInitializer>();

        // Email ingestion: repositories, deduplication, parser (Azure OpenAI via managed identity),
        // and the background processing service.
        builder.Services.AddScoped<IInboxEmailRepository, InboxEmailRepository>();
        builder.Services.AddScoped<IParsedEventDraftRepository, ParsedEventDraftRepository>();
        builder.Services.AddSingleton<EmailDeduplicationService>();
        builder.Services.AddSingleton<AzureOpenAIClient>(_ => CreateOpenAIClient(builder.Configuration));
        builder.Services.AddScoped<EmailParserService>();
        builder.Services.AddHostedService<EmailIngestionBackgroundService>();

        return builder;
    }

    private static TokenCredential CreateDirectoryCredential(IConfiguration configuration)
    {
        var tenantId = configuration["AzureEntra:TenantId"];
        var clientId = configuration["AzureEntra:ClientId"];
        var clientSecret = configuration["AzureEntra:ClientSecret"];

        // App-only access via a client secret: reliable in any environment and requires the
        // application permission User.Read.All (admin-consented) on the app registration.
        if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            return new ClientSecretCredential(tenantId, clientId, clientSecret);
        }

        // Developer / managed-identity fallback. Locally this requires a signed-in identity
        // (for example `az login` or Visual Studio) that can read the tenant directory.
        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            options.TenantId = tenantId;
        }
        return new DefaultAzureCredential(options);
    }

    private static AzureOpenAIClient CreateOpenAIClient(IConfiguration configuration)
    {
        // AzureOpenAI:Endpoint must be set; credential uses DefaultAzureCredential —
        // managed identity when hosted in Azure Container Apps, developer sign-in locally.
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is required for email parsing.");
        return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
    }
}
