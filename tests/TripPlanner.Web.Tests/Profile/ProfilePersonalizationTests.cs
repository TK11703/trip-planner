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
        var client = new RecordingProfileApiClient(ProfileTestData.CompleteProfile(
            personalization: new(null, null, null, null,
                new Dictionary<string, string> { ["activity"] = "purple", ["event"] = "teal" })));
        Services.AddSingleton<IProfileApiClient>(client);
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = Render<ProfilePage>();
        cut.WaitForAssertion(() => Assert.Equal("purple", cut.Find("#color-activity").GetAttribute("value")));
        Assert.Equal("teal", cut.Find("#color-event").GetAttribute("value"));
        Assert.Equal(string.Empty, cut.Find("#color-reminder").GetAttribute("value"));
        Assert.Equal(17, cut.FindAll("#color-activity option").Count);
        cut.Find("#color-activity").Change("gold");
        cut.Find("#color-event").Change("");
        cut.Find("#color-reservation").Change("blue");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(1, client.SaveCallCount));
        var colors = client.LastRequest!.PersonalizationPreferences.ItemTypeColors!;
        Assert.Equal("gold", colors["activity"]);
        Assert.Equal("blue", colors["reservation"]);
        Assert.False(colors.ContainsKey("event"));
        Assert.False(colors.ContainsKey("reminder"));
        Assert.Equal("gold", cut.Find("#color-activity").GetAttribute("value"));
        Assert.Equal(string.Empty, cut.Find("#color-event").GetAttribute("value"));
    }

    [Fact]
    public void ProfilePage_RendersPersonalizationFields()
    {
        var profile = ProfileTestData.CompleteProfile(personalization: new("museums", "SEA", "slow travel", "quiet rooms"));
        Services.AddSingleton<IProfileApiClient>(new RecordingProfileApiClient(profile));
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        var cut = Render<ProfilePage>();

        cut.WaitForAssertion(() => Assert.Equal("museums", cut.Find("#travelInterests").GetAttribute("value")));
        Assert.Equal("SEA", cut.Find("#homeAirport").GetAttribute("value"));
        Assert.Equal("slow travel", cut.Find("#preferredTravelStyle").GetAttribute("value"));
        Assert.Equal("quiet rooms", cut.Find("#accessibilityNotes").GetAttribute("value"));
    }
}
