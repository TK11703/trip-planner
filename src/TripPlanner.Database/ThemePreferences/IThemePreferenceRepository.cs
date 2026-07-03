using TripPlanner.Contracts.Theme;

namespace TripPlanner.Database.ThemePreferences;

public interface IThemePreferenceRepository
{
    Task<ThemePreferenceRecord?> GetAsync(string travelerId, CancellationToken cancellationToken = default);
    Task<ThemePreferenceRecord> UpsertAsync(string travelerId, ThemeMode themeMode, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

public sealed record ThemePreferenceRecord(string TravelerId, ThemeMode ThemeMode, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
