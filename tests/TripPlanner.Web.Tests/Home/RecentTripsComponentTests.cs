using Xunit;

namespace TripPlanner.Web.Tests.Home;

public class RecentTripsComponentTests
{
    [Fact(Skip = "Requires HttpClient mock + authorization context wiring.")]
    public void SignedInUser_WithNoTrips_RendersEmptyState() { }

    [Fact(Skip = "Requires HttpClient mock + authorization context wiring.")]
    public void SignedInUser_WithTrips_RendersCards() { }
}
