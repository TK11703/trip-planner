using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Theme;
using TripPlanner.Web.Components.Layout;
using TripPlanner.Web.Features.Theme;
using Xunit;

namespace TripPlanner.Web.Tests.Theme;

public class ThemeSelectorTests : TestContext
{
    [Fact]
    public void ThemeSelector_RendersLightAndDarkOptions()
    {
        JSInterop.SetupVoid("tripPlannerTheme.applyTheme", _ => true);
        JSInterop.Setup<string>("tripPlannerTheme.getAppliedMode").SetResult("light");
        this.AddTestAuthorization().SetNotAuthorized();
        Services.AddScoped<ThemeStateService>();
        Services.AddSingleton<IThemePreferenceApiClient>(new RecordingThemePreferenceApiClient());
        Services.AddScoped<AccountThemeInitializer>();

        var cut = RenderComponent<ThemeSelector>();

        Assert.Contains("Light", cut.Markup);
        Assert.Contains("Dark", cut.Markup);
        Assert.Contains("aria-label=\"Theme preference\"", cut.Markup);
    }

    private sealed class RecordingThemePreferenceApiClient : IThemePreferenceApiClient
    {
        public Task<ThemePreferenceResponse?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<ThemePreferenceResponse?>(null);
        public Task<ThemePreferenceResponse> SaveAsync(TripPlanner.Contracts.Theme.ThemeMode mode, CancellationToken cancellationToken = default)
            => Task.FromResult(new ThemePreferenceResponse(mode, ThemePreferenceSource.AccountPreference, DateTimeOffset.UtcNow));
    }
}
