using TripPlanner.Contracts.Theme;
using TripPlanner.Web.Features.Theme;
using Xunit;

namespace TripPlanner.Web.Tests.Theme;

public class ThemeApplicationTests
{
    [Fact]
    public void ThemeModeNames_ParseDarkAndFallbackToLight()
    {
        Assert.Equal(TripPlanner.Web.Features.Theme.ThemeMode.Dark, ThemeModeNames.FromCssValue("dark"));
        Assert.Equal(TripPlanner.Web.Features.Theme.ThemeMode.Light, ThemeModeNames.FromCssValue("system"));
    }

    [Fact]
    public void AccountPreferenceSource_IsAvailableForPersistedPreferences()
    {
        var response = new ThemePreferenceResponse(TripPlanner.Contracts.Theme.ThemeMode.Dark, ThemePreferenceSource.AccountPreference, DateTimeOffset.UtcNow);
        Assert.Equal(ThemePreferenceSource.AccountPreference, response.Source);
    }
}
