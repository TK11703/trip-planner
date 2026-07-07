using Xunit;

namespace TripPlanner.E2E.Tests;

public class TimelineFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void AddItems_AppearOnCalendar_MobileAndDesktop() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void AddEventFromLegRow_OpensFormWithLegPreselected() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void AddingEvent_UpdatesOnlySelectedLegEventCount() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void DarkMode_LegBandsRemainVisibleAndLaneStaysClickableBesideEvents() { }
}
