using TripPlanner.Contracts.Theme;

namespace TripPlanner.Database.Tests.ThemePreferences;

public static class ThemePreferenceTestData
{
    public const string TravelerA = "traveler-a-theme";
    public const string TravelerB = "traveler-b-theme";
    public static readonly DateTimeOffset BaselineUpdatedAt = new(2026, 7, 3, 19, 0, 0, TimeSpan.Zero);

    public static IEnumerable<object[]> Modes =>
    [
        [ThemeMode.Light],
        [ThemeMode.Dark]
    ];
}
