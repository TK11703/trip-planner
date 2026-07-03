using Xunit;

namespace TripPlanner.Database.Tests.ThemePreferences;

[Trait("Category", "DatabaseIntegration")]
public class ThemePreferenceRepositoryTests
{
    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void Upsert_PersistsOnePreferencePerTraveler() { }

    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void Get_ReturnsOnlyRequestedTravelerPreference() { }
}
