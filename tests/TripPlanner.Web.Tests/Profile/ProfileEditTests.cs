using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfileEditTests : TestContext
{
    [Fact]
    public void ProfilePage_SavesEditedIdentityAndPreferenceValues()
    {
        var client = new RecordingProfileApiClient(ProfileTestData.CompleteProfile());
        Services.AddSingleton<IProfileApiClient>(client);
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        var cut = RenderComponent<ProfilePage>();

        cut.Find("#displayName").Change("Avery Updated");
        cut.Find("#notif-TripSharing-email").Change(false);
        cut.Find("#travelInterests").Change("food markets");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Profile saved.", cut.Markup));
        Assert.Equal(1, client.SaveCallCount);
        Assert.Equal("Avery Updated", client.LastRequest!.DisplayName);
        Assert.Equal("UTC", client.LastRequest.TimeZoneId);
        Assert.False(client.LastRequest.NotificationPreferences.Find("TripSharing")!.EmailEnabled);
        Assert.Equal("food markets", client.LastRequest.PersonalizationPreferences.TravelInterests);
    }
}
