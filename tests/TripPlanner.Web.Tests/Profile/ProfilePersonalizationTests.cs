using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfilePersonalizationTests : BunitContext
{
    [Fact]
    public void ProfilePage_SavesAndClearsItemTypeColors()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var client = new RecordingProfileApiClient(ProfileTestData.CompleteProfile(
            personalization: new(
                new Dictionary<string, string> { ["activity"] = "purple", ["event"] = "teal" })));
        Services.AddSingleton<IProfileApiClient>(client);
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = Render<ProfilePage>();
        cut.WaitForAssertion(() => Assert.Equal("purple", cut.Find("#color-activity-name").TextContent));
        Assert.Contains("#7c3aed", cut.Find("#color-activity .item-color-preview").GetAttribute("style"));
        Assert.Equal("teal", cut.Find("#color-event-name").TextContent);
        Assert.Equal("No preference (slate)", cut.Find("#color-reminder-name").TextContent);
        Assert.Contains("#64748b", cut.Find("#color-reminder .item-color-preview").GetAttribute("style"));
        var options = cut.FindAll("#color-activity + .item-color-options button");
        Assert.Equal(17, options.Count);
        Assert.All(options, option => Assert.NotNull(option.QuerySelector(".item-color-preview[aria-hidden='true']")));
        Assert.Contains("#e7b85d", cut.Find("#color-activity + .item-color-options [data-color='gold'] .item-color-preview").GetAttribute("style"));
        cut.Find("#color-activity").Click();
        cut.Find("#color-activity + .item-color-options [data-color='gold']").Click();
        cut.Find("#color-event").Click();
        cut.Find("#color-event + .item-color-options [data-color='']").Click();
        cut.Find("#color-reservation").Click();
        cut.Find("#color-reservation + .item-color-options [data-color='blue']").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(1, client.SaveCallCount));
        var colors = client.LastRequest!.PersonalizationPreferences.ItemTypeColors!;
        Assert.Equal("gold", colors["activity"]);
        Assert.Equal("blue", colors["reservation"]);
        Assert.False(colors.ContainsKey("event"));
        Assert.False(colors.ContainsKey("reminder"));
        Assert.Equal("gold", cut.Find("#color-activity-name").TextContent);
        Assert.Contains("#e7b85d", cut.Find("#color-activity .item-color-preview").GetAttribute("style"));
        Assert.Equal("No preference (slate)", cut.Find("#color-event-name").TextContent);
    }

    [Fact]
    public void ProfilePage_OmitsRetiredPersonalizationFields()
    {
        var profile = ProfileTestData.CompleteProfile();
        Services.AddSingleton<IProfileApiClient>(new RecordingProfileApiClient(profile));
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#color-activity")));
        Assert.Empty(cut.FindAll("#travelInterests, #homeAirport, #preferredTravelStyle, #accessibilityNotes"));
        Assert.DoesNotContain("Travel interests", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Home airport", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Preferred travel style", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Accessibility notes", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }
}
