using Microsoft.JSInterop;

namespace TripPlanner.Web.Features.Theme;

public sealed class ThemeStateService
{
    private readonly IJSRuntime _js;

    public ThemeStateService(IJSRuntime js) => _js = js;

    public ThemeMode CurrentMode { get; private set; } = ThemeMode.Light;
    public ThemeModeSource Source { get; private set; } = ThemeModeSource.DeviceBrowser;

    public event Action? Changed;

    public async Task InitializeFromBrowserAsync(CancellationToken cancellationToken = default)
    {
        var mode = await _js.InvokeAsync<string>("tripPlannerTheme.getAppliedMode", cancellationToken);
        CurrentMode = ThemeModeNames.FromCssValue(mode);
        Source = ThemeModeSource.DeviceBrowser;
        Changed?.Invoke();
    }

    public Task ApplyAccountPreferenceAsync(TripPlanner.Contracts.Theme.ThemeMode mode, CancellationToken cancellationToken = default)
        => SetModeAsync(mode == TripPlanner.Contracts.Theme.ThemeMode.Dark ? ThemeMode.Dark : ThemeMode.Light, ThemeModeSource.AccountPreference, persistForVisitor: false, cancellationToken);

    public Task SetModeAsync(ThemeMode mode, ThemeModeSource source, bool persistForVisitor = false, CancellationToken cancellationToken = default)
    {
        CurrentMode = mode;
        Source = source;
        Changed?.Invoke();
        return _js.InvokeVoidAsync("tripPlannerTheme.applyTheme", cancellationToken, mode.ToCssValue(), source.ToSourceValue(), persistForVisitor).AsTask();
    }
}
