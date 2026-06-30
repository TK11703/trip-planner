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

namespace TripPlanner.Api.Extensions;

public static class WebApplicationBuilderExtensions
{
    public const string AuthenticatedUserPolicy = "AuthenticatedUser";

    public static WebApplicationBuilder AddTripPlannerApi(this WebApplicationBuilder builder)
    {
        builder.AddServiceDefaults();
        builder.AddTripPlannerAuthentication();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthenticatedUserPolicy, policy => policy.RequireAuthenticatedUser());
            options.DefaultPolicy = options.GetPolicy(AuthenticatedUserPolicy)!;
        });

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

        builder.Services.AddSingleton<CreateTripValidator>();
        builder.Services.AddSingleton<UpdateTripValidator>();
        builder.Services.AddSingleton<TripLegValidator>();
        builder.Services.AddSingleton<TrackedItemValidator>();

        builder.Services.AddSingleton<DatabaseInitializer>();

        return builder;
    }
}
