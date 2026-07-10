using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Contracts.Profile;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfileNotificationPreferenceTests : TestContext
{
    [Fact]
    public void ProfilePage_RendersSavedNotificationPreferences()
    {
        var preferences = new NotificationPreferences(new[]
        {
            new NotificationCategoryPreference(NotificationCategories.ItineraryChanges, "Itinerary changes", true, false, NotificationPreferenceSource.Saved),
            new NotificationCategoryPreference(NotificationCategories.TripSharing, "Trip sharing", false, true, NotificationPreferenceSource.Saved)
        });
        var profile = ProfileTestData.CompleteProfile(preferences);
        Services.AddSingleton<IProfileApiClient>(new RecordingProfileApiClient(profile));
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = RenderComponent<ProfilePage>();

        cut.WaitForAssertion(() => Assert.True(cut.Find("#notif-ItineraryChanges-inapp").HasAttribute("checked")));
        Assert.False(cut.Find("#notif-ItineraryChanges-email").HasAttribute("checked"));
        Assert.False(cut.Find("#notif-TripSharing-inapp").HasAttribute("checked"));
        Assert.True(cut.Find("#notif-TripSharing-email").HasAttribute("checked"));
    }

    [Fact]
    public void ProfilePage_RendersSaveValidationErrors()
    {
        Services.AddSingleton<IProfileApiClient>(new ThrowingProfileApiClient(ProfileTestData.CompleteProfile()));
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        var cut = RenderComponent<ProfilePage>();

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Enter a valid email address.", cut.Markup));
    }
}
