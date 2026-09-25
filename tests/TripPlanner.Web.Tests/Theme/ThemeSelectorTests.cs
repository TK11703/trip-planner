using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using TripPlanner.Contracts.Theme;
using TripPlanner.Web.Components.Layout;
using TripPlanner.Web.Features.Theme;
using Xunit;

namespace TripPlanner.Web.Tests.Theme;

public class ThemeSelectorTests : BunitContext
{
    [Fact]
    public void ThemeSelector_RendersLightAndDarkOptions()
    {
        JSInterop.SetupVoid("tripPlannerTheme.applyTheme", _ => true);
        JSInterop.Setup<string>("tripPlannerTheme.getAppliedMode").SetResult("light");
        this.AddAuthorization().SetNotAuthorized();
        Services.AddScoped<ThemeStateService>();
        Services.AddSingleton<IThemePreferenceApiClient>(new RecordingThemePreferenceApiClient());
        Services.AddScoped<AccountThemeInitializer>();

        var cut = Render<ThemeSelector>();

        Assert.Contains("Light", cut.Markup);
        Assert.Contains("Dark", cut.Markup);
        Assert.Contains("aria-label=\"Theme preference\"", cut.Markup);
    }

    [Fact]
    public void ThemeSelector_SurvivesTokenChallengeWhenLoadingAccountPreference()
    {
        // A resumed circuit on a process that lost its token cache throws a challenge from the
        // preference lookup; the footer widget must fall back to the browser theme instead of
        // tearing down the whole circuit.
        JSInterop.SetupVoid("tripPlannerTheme.applyTheme", _ => true);
        var getApplied = JSInterop.Setup<string>("tripPlannerTheme.getAppliedMode");
        getApplied.SetResult("dark");
        this.AddAuthorization().SetAuthorized("traveler");
        Services.AddScoped<ThemeStateService>();
        Services.AddSingleton<IThemePreferenceApiClient>(new ThrowingThemePreferenceApiClient(
            new MicrosoftIdentityWebChallengeUserException(
                new MsalUiRequiredException("invalid_grant", "interaction required"), ["api://trip-planner/access_as_user"])));
        Services.AddScoped<AccountThemeInitializer>();

        var cut = Render<ThemeSelector>();

        cut.WaitForAssertion(() => Assert.DoesNotContain("theme-selector-loading", cut.Markup, StringComparison.Ordinal));
        Assert.Single(getApplied.Invocations);
        Assert.Equal(TripPlanner.Web.Features.Theme.ThemeMode.Dark, Services.GetRequiredService<ThemeStateService>().CurrentMode);
    }

    private sealed class ThrowingThemePreferenceApiClient : IThemePreferenceApiClient
    {
        private readonly Exception _failure;
        public ThrowingThemePreferenceApiClient(Exception failure) => _failure = failure;
        public Task<ThemePreferenceResponse?> GetAsync(CancellationToken cancellationToken = default) => Task.FromException<ThemePreferenceResponse?>(_failure);
        public Task<ThemePreferenceResponse> SaveAsync(TripPlanner.Contracts.Theme.ThemeMode mode, CancellationToken cancellationToken = default) => Task.FromException<ThemePreferenceResponse>(_failure);
    }

    private sealed class RecordingThemePreferenceApiClient : IThemePreferenceApiClient
    {
        public Task<ThemePreferenceResponse?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<ThemePreferenceResponse?>(null);
        public Task<ThemePreferenceResponse> SaveAsync(TripPlanner.Contracts.Theme.ThemeMode mode, CancellationToken cancellationToken = default)
            => Task.FromResult(new ThemePreferenceResponse(mode, ThemePreferenceSource.AccountPreference, DateTimeOffset.UtcNow));
    }
}
