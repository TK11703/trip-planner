using Xunit;

namespace TripPlanner.E2E.Tests;

// Feature 019: Trip Location Maps — profile default map, provider-aware globe, and the
// built-in trip map. Mirrors the repo's E2E convention (Playwright, skipped without a
// running AppHost).
public class TripLocationMapFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void ChangingDefaultMap_DrivesGlobeProvider() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void BuiltInMap_OpensWithMarkersForTripWithLocations() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void ViewMap_DisabledWhenTripHasNoLocations() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void SelectingMarker_OpensEventDetails() { }
}
