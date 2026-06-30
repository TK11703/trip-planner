using Xunit;

namespace TripPlanner.E2E.Tests;

public class PublicNavigationTests
{
    [Fact(Skip = "Playwright browser tests require running AppHost; enable in E2E pipeline.")]
    public void Landing_FAQ_About_Navigate_OnMobileAndDesktop() { }
}
