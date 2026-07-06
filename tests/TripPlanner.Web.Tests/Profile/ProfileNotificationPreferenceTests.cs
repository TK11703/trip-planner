using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfileNotificationPreferenceTests : TestContext
{
    [Fact]
    public void ProfilePage_RendersSavedNotificationPreferences()
    {
        var profile = ProfileTestData.CompleteProfile(new(true, true, false));
        Services.AddSingleton<IProfileApiClient>(new RecordingProfileApiClient(profile));
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = RenderComponent<ProfilePage>();

        cut.WaitForAssertion(() => Assert.True(cut.Find("#emailNotifications").HasAttribute("checked")));
        Assert.True(cut.Find("#tripReminders").HasAttribute("checked"));
        Assert.False(cut.Find("#itineraryChanges").HasAttribute("checked"));
    }

    [Fact]
    public void ProfilePage_RendersSaveValidationErrors()
    {
        Services.AddSingleton<IProfileApiClient>(new ThrowingProfileApiClient(ProfileTestData.CompleteProfile()));
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        var cut = RenderComponent<ProfilePage>();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Enter a valid email address.", cut.Markup));
    }
}
