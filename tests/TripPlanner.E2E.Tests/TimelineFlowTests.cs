using Xunit;

namespace TripPlanner.E2E.Tests;

public class TimelineFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void AddItems_AppearOnCalendar_MobileAndDesktop() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void AddItemFromLegRow_OpensFormWithLegPreselected() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void AddingItem_UpdatesOnlySelectedLegItemCount() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void DarkMode_LegBandsRemainVisibleAndLaneStaysClickableBesideItems() { }
}
