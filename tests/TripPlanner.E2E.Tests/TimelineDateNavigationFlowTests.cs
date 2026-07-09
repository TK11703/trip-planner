using Xunit;

namespace TripPlanner.E2E.Tests;

// Feature 014: Timeline date navigation. These mirror the repo's existing timeline
// E2E convention (Playwright, skipped without a running AppHost).
public class TimelineDateNavigationFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void JumpToDate_ScrollsSelectedDayToLeftEdge() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void NextDay_StopsAtTripEndBoundary() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void PreviousDay_StopsAtTripStartBoundary() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void TodayShortcut_HiddenWhenTodayOutsideTripRange() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void SingleDayTrip_NavigationStaysOnTheOneDay() { }
}
