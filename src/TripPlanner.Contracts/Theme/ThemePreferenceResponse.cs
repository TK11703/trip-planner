namespace TripPlanner.Contracts.Theme;

public sealed record ThemePreferenceResponse(ThemeMode ThemeMode, ThemePreferenceSource Source, DateTimeOffset UpdatedAtUtc);
