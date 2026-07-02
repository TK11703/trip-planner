using Microsoft.AspNetCore.Authorization;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Components.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Auth;

public class ProtectedRouteTests
{
    [Fact]
    public void NewTripPage_RequiresAuthorization()
    {
        Assert.Contains(typeof(NewTrip).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true), attribute => attribute is AuthorizeAttribute);
    }

    [Fact]
    public void TripDetailsPage_RequiresAuthorization()
    {
        Assert.Contains(typeof(TripDetails).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true), attribute => attribute is AuthorizeAttribute);
    }

    [Fact]
    public void SignInRequiredState_RendersRecoveryAction()
    {
        using var ctx = new Bunit.TestContext();
        var cut = ctx.RenderComponent<TripAccessState>(parameters => parameters.Add(p => p.State, TripAccessState.TripAccessStateKind.SignInRequired));

        Assert.Contains("Sign in required", cut.Markup);
        Assert.Contains("MicrosoftIdentity/Account/SignIn", cut.Markup);
    }
}
