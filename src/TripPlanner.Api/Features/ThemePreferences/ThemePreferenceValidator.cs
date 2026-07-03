using TripPlanner.Contracts.Theme;

namespace TripPlanner.Api.Features.ThemePreferences;

public sealed class ThemePreferenceValidator
{
    public bool IsValid(UpdateThemePreferenceRequest? request)
        => request?.ThemeMode is not null && Enum.IsDefined(request.ThemeMode.Value);
}
