using TripPlanner.Web.Components;

namespace TripPlanner.Web.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseTripPlannerWeb(this WebApplication app)
    {
        app.MapDefaultEndpoints();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAntiforgery();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        return app;
    }
}
