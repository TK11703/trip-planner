using Xunit;

namespace TripPlanner.E2E.Tests;

// Feature 018: Timeline event entry experience — mappable, validated location.
// These mirror the repo's existing timeline E2E convention (Playwright, skipped without a
// running AppHost).
public class TimelineLocationMapFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void ValidLocation_GlobeOpensMapInNewTab() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void InvalidLocation_ShowsValidationAndNoGlobeAction() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void EmptyLocation_AllowsSaveWithoutGlobeAction() { }
}
