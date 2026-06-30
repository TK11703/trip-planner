using TripPlanner.Api.Features.Trips;
using TripPlanner.Api.Features.TripItems;
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
        app.UseAuthorization();

        app.MapTripEndpoints();
        app.MapTripItemEndpoints();

        return app;
    }

    public static async Task<WebApplication> InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await initializer.InitializeAsync(scope.ServiceProvider, cancellationToken);
        return app;
    }
}
