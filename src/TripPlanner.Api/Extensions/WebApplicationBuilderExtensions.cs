using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using TripPlanner.Api.Security;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Timeline;
using TripPlanner.Database.Initialization;
using TripPlanner.Contracts.Common;
using TripPlanner.Api.Features.Trips.CreateTrip;
using TripPlanner.Api.Features.Trips.UpdateTrip;
using TripPlanner.Api.Features.TripItems;
using TripPlanner.Database.ThemePreferences;
using TripPlanner.Api.Features.ThemePreferences;
using TripPlanner.Api.Features.UserProfiles;
using TripPlanner.Api.Features.Timezones;
using TripPlanner.Database.UserProfiles;

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
        builder.Services.AddScoped<IAuditRepository, AuditRepository>();
        builder.Services.AddScoped<IThemePreferenceRepository, ThemePreferenceRepository>();
        builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();

        builder.Services.AddSingleton<CreateTripValidator>();
        builder.Services.AddSingleton<UpdateTripValidator>();
        builder.Services.AddSingleton<TripLegValidator>();
        builder.Services.AddSingleton<TrackedItemValidator>();
        builder.Services.AddSingleton<ThemePreferenceValidator>();
        builder.Services.AddSingleton<UserProfileValidator>();
        builder.Services.AddSingleton<ITimezoneIdValidator, TimezoneIdValidator>();

        builder.Services.AddSingleton<DatabaseInitializer>();

        return builder;
    }
}
