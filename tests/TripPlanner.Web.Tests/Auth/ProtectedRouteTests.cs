using Xunit;

namespace TripPlanner.Web.Tests.Auth;

public class ProtectedRouteTests
{
    [Fact(Skip = "Requires bUnit AuthenticationStateProvider wiring per route under test.")]
    public void NewTripPage_RedirectsAnonymousUsersToSignIn() { }

    [Fact(Skip = "Requires bUnit AuthenticationStateProvider wiring per route under test.")]
    public void TripDetailsPage_RedirectsAnonymousUsersToSignIn() { }
}
