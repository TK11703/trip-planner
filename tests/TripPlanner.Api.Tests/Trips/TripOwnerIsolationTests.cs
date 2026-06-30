using Xunit;

namespace TripPlanner.Api.Tests.Trips;

/// <summary>
/// Placeholder for owner-isolation contract tests. Wiring a real Testcontainers-backed
/// PostgreSQL fixture into <see cref="TripPlanner.Api.Tests.Infrastructure.TestApiFactory"/>
/// is required to exercise the full path end-to-end.
/// </summary>
public class TripOwnerIsolationTests
{
    [Fact(Skip = "Requires Testcontainers-backed Postgres wired into TestApiFactory.")]
    public void UserB_CannotReadUserATripById() { }

    [Fact(Skip = "Requires Testcontainers-backed Postgres wired into TestApiFactory.")]
    public void RecentTrips_OnlyReturnsCurrentUserTrips() { }
}
