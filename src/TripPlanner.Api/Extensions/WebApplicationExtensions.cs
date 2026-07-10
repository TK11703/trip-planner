using TripPlanner.Api.Features.Trips;
using TripPlanner.Api.Features.TripItems;
using TripPlanner.Api.Features.TripMaps;
using TripPlanner.Api.Features.TripSharing;
using TripPlanner.Api.Features.ThemePreferences;
using TripPlanner.Api.Features.UserProfiles;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Api.Features.Places;
using TripPlanner.Contracts.Audit;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Initialization;

namespace TripPlanner.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseTripPlannerApi(this WebApplication app)
    {
        app.MapDefaultEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            await next();

            if ((context.Response.StatusCode == StatusCodes.Status401Unauthorized
                    || context.Response.StatusCode == StatusCodes.Status403Forbidden)
                && context.Request.Path.StartsWithSegments("/api/trips"))
            {
                await RecordDeniedAccessOutcomeAsync(context);
            }
        });
        app.UseAuthorization();

        app.MapTripEndpoints();
        app.MapTripItemEndpoints();
        app.MapTripMapEndpoints();
        app.MapTripSharingEndpoints();
        app.MapThemePreferenceEndpoints();
        app.MapUserProfileEndpoints();
        app.MapNotificationEndpoints();
        app.MapPlaceEndpoints();

        return app;
    }

    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(scope.ServiceProvider, cancellationToken);
        return app;
    }

    private static async Task RecordDeniedAccessOutcomeAsync(HttpContext context)
    {
        try
        {
            using var scope = context.RequestServices.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditRepository>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TripPlanner.Api.SecurityAudit");

            var resourceId = context.Request.RouteValues.TryGetValue("tripId", out var value) ? value?.ToString() : null;
            var result = context.User.Identity?.IsAuthenticated == true ? AuditResults.Denied : AuditResults.Unauthenticated;
            await audit.RecordAsync(
                userId: null,
                operation: AuditOperations.AccessDenied,
                resourceType: "trip",
                resourceId: resourceId,
                result: result,
                occurredAtUtc: DateTimeOffset.UtcNow,
                cancellationToken: context.RequestAborted);
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("TripPlanner.Api.SecurityAudit");
            logger?.LogDebug(ex, "Unable to record denied protected-data access outcome.");
        }
    }
}
