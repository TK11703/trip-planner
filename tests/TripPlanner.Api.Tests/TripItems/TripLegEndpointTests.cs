using Xunit;

namespace TripPlanner.Api.Tests.TripItems;

public class TripLegEndpointTests
{
    [Fact(Skip = "Requires DB-backed integration via Testcontainers.")]
    public void CreateLeg_RequiresOwnedTrip() { }
}
