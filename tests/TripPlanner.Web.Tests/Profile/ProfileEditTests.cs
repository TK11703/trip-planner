using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfileEditTests : BunitContext
{
    [Fact]
    public void ProfilePage_SavesEditedIdentityAndPreferenceValues()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var client = new RecordingProfileApiClient(ProfileTestData.CompleteProfile());
        Services.AddSingleton<IProfileApiClient>(client);
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        var cut = Render<ProfilePage>();

        Assert.Equal("Profile", cut.Find("header.entry-page-heading h1").TextContent);
        Assert.Single(cut.FindAll("h1"));
        Assert.Equal("-1", cut.Find("h1").GetAttribute("tabindex"));
        Assert.Equal("profile", cut.FindComponent<EntryPageHeading>().Instance.Icon);
        Assert.NotNull(cut.Find(".entry-page-icon svg[aria-hidden='true'] circle"));
        Assert.Contains("entry-form-card", cut.Find("form").GetAttribute("class"));
        var saveButton = Assert.Single(cut.FindAll("form > .card-footer.entry-form-footer > .entry-form-actions > button[type='submit']"));
        Assert.Equal("Save", saveButton.TextContent.Trim());
        cut.Find("#displayName").Change("Avery Updated");
        cut.Find("#notif-TripSharing-email").Change(false);
        cut.Find("#color-activity").Click();
        cut.Find("#color-activity + .item-color-options [data-color='teal']").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Profile saved.", cut.Markup));
        Assert.Equal(1, client.SaveCallCount);
        Assert.Equal("Avery Updated", client.LastRequest!.DisplayName);
        Assert.Equal("UTC", client.LastRequest.TimeZoneId);
        Assert.False(client.LastRequest.NotificationPreferences.Find("TripSharing")!.EmailEnabled);
        Assert.Equal("teal", client.LastRequest.PersonalizationPreferences.ItemTypeColors!["activity"]);
    }
}
