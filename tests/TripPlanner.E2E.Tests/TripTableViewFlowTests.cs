using Xunit;

namespace TripPlanner.E2E.Tests;

public class TripTableViewFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void KeyboardActivatesTableActions() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void EditModalReturnsFocusToInvokingTableAction() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void ViewerSeesTableDataWithoutEditControls() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void NarrowViewportKeepsEveryColumnHorizontallyReachable() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void LongValuesWrapWithoutOverlappingAdjacentRows() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void TableControlsAndContentDoNotOverlapAtSupportedViewports() { }
}