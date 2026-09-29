using Xunit;

namespace TripPlanner.E2E.Tests.FavoriteDestinations;

public sealed class FavoriteDestinationMaintenanceFlowTests
{
    [Fact(Skip = "Playwright; requires signed-in AppHost flow, which is not available in this test environment.")]
    public void SignedInTraveler_SearchesEditsAndConfirmsFavoriteDeletion() { }
}