using Xunit;

namespace TripPlanner.Database.Tests.Trips;

[Trait("Category", "DatabaseIntegration")]
public class TripCommandTests
{
    [Fact(Skip = "Requires Docker/Testcontainers.")]
    public void InsertTrip_PersistsOwnerUserId() { }

    [Fact(Skip = "Requires Docker/Testcontainers.")]
    public void UpdateTrip_RejectsCrossOwner() { }
}
