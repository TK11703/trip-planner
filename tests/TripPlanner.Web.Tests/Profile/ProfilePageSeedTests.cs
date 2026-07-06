using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfilePageSeedTests : TestContext
{
    [Fact]
    public void SignedInUser_DisplaysAzureSeededProfileValues()
    {
        Services.AddSingleton<IProfileApiClient>(new RecordingProfileApiClient(ProfileTestData.CompleteProfile()));
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = RenderComponent<ProfilePage>();

        cut.WaitForAssertion(() => Assert.Contains("Avery Traveler", cut.Markup));
        Assert.Equal("Avery", cut.Find("#firstName").GetAttribute("value"));
        Assert.Equal("Traveler", cut.Find("#lastName").GetAttribute("value"));
        Assert.Equal("avery@example.test", cut.Find("#email").GetAttribute("value"));
    }
}
