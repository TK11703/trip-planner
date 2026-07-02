using Xunit;

namespace TripPlanner.E2E.Tests;

public class AuthenticatedApiCallFlowTests
{
    [Fact(Skip = "Playwright browser tests require running AppHost and Azure Entra test users; enable in E2E pipeline.")]
    public void SignedInRecentTripsAndCreateTrip_UseBearerAuthenticatedApiCalls() { }

    [Fact(Skip = "Playwright browser tests require running AppHost and Azure Entra test users; enable in E2E pipeline.")]
    public void CrossUserDirectTripUrl_IsDeniedWithoutDisclosure() { }
}
