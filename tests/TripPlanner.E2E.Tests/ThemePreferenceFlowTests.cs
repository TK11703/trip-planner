using Xunit;

namespace TripPlanner.E2E.Tests;

public class ThemePreferenceFlowTests
{
    [Fact(Skip = "Playwright flow requires running Aspire AppHost and authenticated test accounts.")]
    public void SignedInThemePreference_PersistsAcrossClients_AndFallsBackToBrowserDefault() { }
}
