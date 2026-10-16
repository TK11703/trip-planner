using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Features.Profile;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Tests.Auth;
using ProfilePage = TripPlanner.Web.Components.Pages.Profile;

namespace TripPlanner.Web.Tests.Profile;

public class ProfileSaveConfirmationTests : BunitContext
{
    private IRenderedComponent<ProfilePage> RenderProfile(IProfileApiClient? client = null)
    {
        Services.AddSingleton(client ?? new RecordingProfileApiClient(ProfileTestData.CompleteProfile()));
        Services.AddSingleton<ITimezoneOptionsProvider, TimezoneOptionsProvider>();
        Services.AddSingleton<TripPlanner.Web.Features.Maps.IMapPreferenceProvider, TripPlanner.Web.Features.Maps.MapPreferenceProvider>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        return Render<ProfilePage>();
    }

    [Fact]
    public async Task SuccessfulSave_ShowsCheckBeforeMessage_AndDismissesAfterTenSeconds()
    {
        var cut = RenderProfile();
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".alert-success[role='status']")));

        var message = cut.Find(".alert-success");
        Assert.Equal("svg", message.FirstElementChild!.LocalName);
        Assert.Equal("true", message.FirstElementChild.GetAttribute("aria-hidden"));
        Assert.Contains("Profile saved.", message.QuerySelector("span")!.TextContent);
        Assert.Contains(cut.FindComponents<ActionIcon>(), icon => icon.Instance.Name == "success");

        await Task.Delay(TimeSpan.FromSeconds(9));
        Assert.NotNull(cut.Find(".alert-success"));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".alert-success")), timeout: TimeSpan.FromSeconds(3));
        Assert.NotNull(cut.Find("form"));
    }

    [Fact]
    public async Task AnotherSuccessfulSave_RestartsTheTenSecondCountdown()
    {
        var cut = RenderProfile();
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".alert-success")));
        await Task.Delay(TimeSpan.FromSeconds(6));

        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".alert-success")));
        await Task.Delay(TimeSpan.FromSeconds(5));

        Assert.NotNull(cut.Find(".alert-success"));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".alert-success")), timeout: TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void FailedSave_ClearsPreviousSuccessAndShowsErrorInstead()
    {
        var fail = false;
        var profile = ProfileTestData.CompleteProfile();
        var client = new RecordingProfileApiClient(profile, _ =>
            fail ? throw new InvalidOperationException("Unable to save profile.") : profile);
        var cut = RenderProfile(client);
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".alert-success")));

        fail = true;
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Unable to save profile.", cut.Find(".alert-danger").TextContent));
        Assert.Empty(cut.FindAll(".alert-success"));
    }

    [Fact]
    public async Task NavigatingAway_CancelsPendingDismissal()
    {
        var cut = RenderProfile();
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".alert-success")));

        cut.Dispose();

        await Task.Delay(TimeSpan.FromSeconds(11));
        Assert.False(Renderer.UnhandledException.IsCompleted);
    }
}
