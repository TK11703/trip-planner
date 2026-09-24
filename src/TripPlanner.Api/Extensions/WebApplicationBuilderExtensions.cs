using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Health;
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
        // Singleton: the factory owns an NpgsqlDataSource, and a per-scope instance would
        // create a separate connection pool and token cache for every request.
        builder.Services.AddSingleton<IPostgresConnectionFactory, PostgresConnectionFactory>();
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

        // Azure Maps address/place typeahead + geocoding, authenticated with Entra ID rather than a
        // shared key. The credential is keyed so it stays separate from the Graph credential above,
        // which may be a client secret that carries no Maps role. Both capabilities degrade
        // gracefully when AzureMaps:ClientId is unset.
        builder.Services.AddKeyedSingleton<TokenCredential>(
            AzureMapsPlaceSuggestionLookup.HttpClientName,
            (_, _) => new DefaultAzureCredential());
        builder.Services.AddHttpClient(AzureMapsPlaceSuggestionLookup.HttpClientName, client =>
        {
            var endpoint = builder.Configuration["AzureMaps:Endpoint"];
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(endpoint) ? "https://atlas.microsoft.com/" : endpoint);
        });
        builder.Services.AddScoped<AzureMapsPlaceSuggestionLookup>(sp => new AzureMapsPlaceSuggestionLookup(
            sp.GetRequiredService<IHttpClientFactory>(),
            builder.Configuration,
            sp.GetRequiredKeyedService<TokenCredential>(AzureMapsPlaceSuggestionLookup.HttpClientName),
            sp.GetRequiredService<ILogger<AzureMapsPlaceSuggestionLookup>>()));
        builder.Services.AddScoped<IPlaceSuggestionLookup>(sp => sp.GetRequiredService<AzureMapsPlaceSuggestionLookup>());
        builder.Services.AddScoped<IPlaceGeocoder>(sp => sp.GetRequiredService<AzureMapsPlaceSuggestionLookup>());
        builder.Services.AddScoped<IPlaceTimeZoneLookup>(sp => sp.GetRequiredService<AzureMapsPlaceSuggestionLookup>());

        builder.Services.AddSingleton<DatabaseInitializer>();
        builder.Services.AddSingleton<DatabaseMigrationState>();

        // Readiness checks. None are tagged "live", so /alive stays dependency-free and a
        // failing dependency removes the replica from traffic instead of restarting it.
        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name, tags: ["ready"])
            .AddCheck<MigrationHealthCheck>(MigrationHealthCheck.Name, tags: ["ready"])
            .AddCheck<AzureOpenAIConfigurationHealthCheck>(AzureOpenAIConfigurationHealthCheck.Name, tags: ["ready"]);

        // Email ingestion: repositories, deduplication, sender resolution, attachment text
        // extraction, and the recognition parser (Azure OpenAI via managed identity).
        // Messages are processed synchronously inside the relay request — there is no
        // hosted service, timer, or mailbox client anywhere in the API.
        builder.Services.AddScoped<IInboxEmailRepository, InboxEmailRepository>();
        builder.Services.AddScoped<IEmailAttachmentRepository, EmailAttachmentRepository>();
        builder.Services.AddScoped<IParsedItemDraftRepository, ParsedItemDraftRepository>();
        builder.Services.AddSingleton<EmailDeduplicationService>();
        builder.Services.AddSingleton<EmailAttachmentTextExtractor>();
        builder.Services.AddSingleton<DraftPlacementMatcher>();
        builder.Services.AddScoped<EmailSenderResolver>();
        builder.Services.AddSingleton<AzureOpenAIClient>(_ => CreateOpenAIClient(builder.Configuration));
        builder.Services.AddScoped<IItemRecognizer, EmailParserService>();
        builder.Services.AddScoped<RelayMessageProcessor>();

        // A factory, not the recognizer itself: re-recognition must survive a provider that
        // cannot even be constructed, and construction happens during resolution (FR-047).
        builder.Services.AddScoped<Func<IItemRecognizer>>(sp => sp.GetRequiredService<IItemRecognizer>);
        builder.Services.AddScoped<DraftReRecognitionService>();

        return builder;
    }

    private static TokenCredential CreateDirectoryCredential(ConfigurationManager configuration)
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

    private static AzureOpenAIClient CreateOpenAIClient(ConfigurationManager configuration)
    {
        // AzureOpenAI:Endpoint must be set; credential uses DefaultAzureCredential —
        // managed identity when hosted, developer sign-in locally.
        //
        // Which developer credential answers locally is not decided here. DefaultAzureCredential
        // reaches VisualStudioCredential before AzureCliCredential, and Visual Studio is often
        // signed in as a different account than `az login`; that credential then returns a valid
        // token the data plane rejects with a 401, which reads as a configuration error rather
        // than an identity mismatch. The AppHost pins the chain for local runs by setting
        // AZURE_TOKEN_CREDENTIALS, so the choice lives in orchestration where it belongs and this
        // code stays the same in every environment.
        var endpoint = configuration["AzureOpenAI:Endpoint"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is required for email parsing.");

        return new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
    }
}
