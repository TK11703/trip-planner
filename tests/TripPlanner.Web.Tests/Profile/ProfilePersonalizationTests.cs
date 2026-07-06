using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfilePersonalizationTests : TestContext
{
    [Fact]
    public void ProfilePage_RendersPersonalizationFields()
    {
        var profile = ProfileTestData.CompleteProfile(personalization: new("museums", "SEA", "slow travel", "quiet rooms"));
        Services.AddSingleton<IProfileApiClient>(new RecordingProfileApiClient(profile));
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = RenderComponent<ProfilePage>();

        cut.WaitForAssertion(() => Assert.Equal("museums", cut.Find("#travelInterests").GetAttribute("value")));
        Assert.Equal("SEA", cut.Find("#homeAirport").GetAttribute("value"));
        Assert.Equal("slow travel", cut.Find("#preferredTravelStyle").GetAttribute("value"));
        Assert.Equal("quiet rooms", cut.Find("#accessibilityNotes").GetAttribute("value"));
    }
}
