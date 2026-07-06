using Xunit;

namespace TripPlanner.E2E.Tests;

public class ProfileFlowTests
{
    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void SignIn_FirstProfileLoad_SeedsProfileFromAuthenticatedClaims() { }

    [Fact(Skip = "Playwright; requires running AppHost.")]
    public void EditProfile_ReturnSignIn_ShowsPersistedValues() { }
}
