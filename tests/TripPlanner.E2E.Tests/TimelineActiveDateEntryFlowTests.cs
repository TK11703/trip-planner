using Xunit;

namespace TripPlanner.E2E.Tests;

// Feature 018: Timeline event entry experience — active-date tracking and date defaults.
// These mirror the repo's existing timeline E2E convention (Playwright, skipped without a
// running AppHost).
public class TimelineActiveDateEntryFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void ScrollingTimeline_UpdatesActiveDateToCenteredDay() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void JumpToDate_KeepsActiveDateAsSelectedDay() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void AddEvent_DefaultsStartToActiveDate_AndEndToOneHourLater() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void ChangingStartInEventForm_AdjustsEndToOneHourLater() { }
}
