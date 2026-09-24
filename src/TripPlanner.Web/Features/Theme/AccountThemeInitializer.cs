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

        TripPlanner.Contracts.Theme.ThemePreferenceResponse? preference;
        try
        {
            preference = await _client.GetAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The theme is cosmetic and already applied from the cookie at first paint, so a lost
            // token (e.g. the circuit resumed on a restarted process) or an API outage must not
            // tear down the circuit. The page's own data calls handle re-authentication.
            preference = null;
        }

        if (preference is null)
        {
            await _themeState.InitializeFromBrowserAsync(cancellationToken);
            return;
        }

        await _themeState.ApplyAccountPreferenceAsync(preference.ThemeMode, cancellationToken);
    }
}
