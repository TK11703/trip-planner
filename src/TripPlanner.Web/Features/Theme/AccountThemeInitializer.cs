using Microsoft.AspNetCore.Components.Authorization;

namespace TripPlanner.Web.Features.Theme;

public sealed class AccountThemeInitializer
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IThemePreferenceApiClient _client;
    private readonly ThemeStateService _themeState;

    public AccountThemeInitializer(AuthenticationStateProvider authenticationStateProvider, IThemePreferenceApiClient client, ThemeStateService themeState)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _client = client;
        _themeState = themeState;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var auth = await _authenticationStateProvider.GetAuthenticationStateAsync();
        if (auth.User.Identity?.IsAuthenticated != true)
        {
            await _themeState.InitializeFromBrowserAsync(cancellationToken);
            return;
        }

        var preference = await _client.GetAsync(cancellationToken);
        if (preference is null)
        {
            await _themeState.InitializeFromBrowserAsync(cancellationToken);
            return;
        }

        await _themeState.ApplyAccountPreferenceAsync(preference.ThemeMode, cancellationToken);
    }
}
