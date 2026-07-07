using Xunit;

namespace TripPlanner.Database.Tests.TripSharing;

[Trait("Category", "DatabaseIntegration")]
public class TripSharingRepositoryTests
{
    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void UpsertShare_PersistsOneAccessLevelPerMember() { }

    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void UpsertShare_SecondCall_UpdatesAccessLevelInsteadOfDuplicating() { }

    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void GetAccess_ReturnsOwnerForTripOwner_AndShareLevelForMember() { }

    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void GetTripsPage_IncludesOwnedAndSharedTrips() { }

    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void DeleteShare_RemovesMemberAccess() { }
}
