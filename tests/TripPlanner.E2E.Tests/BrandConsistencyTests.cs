using Xunit;

namespace TripPlanner.E2E.Tests;

public class BrandConsistencyTests
{
    [Fact(Skip = "Playwright flow requires running Aspire AppHost.")]
    public void PublicAndSignedInSurfaces_UseSharedAdventureBrand() { }
}
