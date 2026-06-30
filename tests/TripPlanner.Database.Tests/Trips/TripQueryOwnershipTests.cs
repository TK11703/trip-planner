using Xunit;

namespace TripPlanner.Database.Tests.Trips;

[Trait("Category", "DatabaseIntegration")]
public class TripQueryOwnershipTests
{
    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void GetRecentTrips_FiltersByOwnerUserId() { }

    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void GetTripDetail_ReturnsNullForOtherOwner() { }
}
