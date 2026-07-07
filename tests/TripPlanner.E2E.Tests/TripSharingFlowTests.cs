using Xunit;

namespace TripPlanner.E2E.Tests;

public class TripSharingFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost and a configured tenant directory.")]
    public void Owner_SharesTrip_AsViewerAndCollaborator() { }

    [Fact(Skip = "Playwright; requires running AppHost and a configured tenant directory.")]
    public void Viewer_SeesSharedBadge_AndCannotEdit() { }

    [Fact(Skip = "Playwright; requires running AppHost and a configured tenant directory.")]
    public void Collaborator_CanEditItinerary_ButNotManageSharing() { }

    [Fact(Skip = "Playwright; requires running AppHost and a configured tenant directory.")]
    public void Owner_RemovesShare_RevokesAccessOnNextAction() { }
}
