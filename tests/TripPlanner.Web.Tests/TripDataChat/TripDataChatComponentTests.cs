using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.TripDataChat;
using TripPlanner.Web.Components.Layout;
using TripPlanner.Web.Features.TripDataChat;
using ChatComponent = TripPlanner.Web.Components.Layout.TripDataChat;

namespace TripPlanner.Web.Tests.TripDataChat;

public sealed class TripDataChatComponentTests : BunitContext
{
    [Fact]
    public void AnonymousUserGetsNoActivatorOrChatApiCalls()
    {
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(new TripChatSessionStore(JSInterop.JSRuntime));
        Services.AddSingleton<AuthenticationStateProvider>(new ChatAuthenticationStateProvider(false, null));
        var api = new StubChatApiClient();
        Services.AddSingleton<ITripDataChatApiClient>(api);

        var cut = Render<ChatComponent>();

        Assert.Empty(cut.FindAll(".trip-chat-activator"));
        Assert.Equal(0, api.CallCount);
    }

    [Fact]
    public async Task AuthenticatedUserOpensAccessiblePaneAndRendersTextCitationsSafely()
    {
        const string epoch = "opaque-epoch-1";
        var citation = new TripDataChatCitation(Guid.NewGuid(), "Pacific trip", TripDataChatSourceKind.Leg, Guid.NewGuid(), "Flight to Seattle");
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(new TripChatSessionStore(JSInterop.JSRuntime));
        Services.AddSingleton<AuthenticationStateProvider>(new ChatAuthenticationStateProvider(true, epoch));
        var api = new StubChatApiClient
        {
            Response = new TripDataChatResponse(TripDataChatStatus.Answered, "<b>Answer</b>", [citation])
        };
        Services.AddSingleton<ITripDataChatApiClient>(api);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./js/tripDataChat.js");
        module.Setup<TripChatSessionSnapshot>("load", epoch).SetResult(TripChatSessionSnapshot.Empty);

        var cut = Render<ChatComponent>();
        cut.WaitForAssertion(() => Assert.Equal("false", cut.Find(".trip-chat-activator").GetAttribute("aria-expanded")));
        Assert.Equal("trip-data-chat-panel", cut.Find(".trip-chat-activator").GetAttribute("aria-controls"));

        await cut.Find(".trip-chat-activator").ClickAsync();
        Assert.Single(cut.FindAll("#trip-data-chat-panel"));
        Assert.NotEmpty(cut.FindAll("label[for='trip-chat-message']"));

        cut.Find("#trip-chat-message").Input("When does the first leg start?");
        await cut.Find("button[type='submit']").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("<b>Answer</b>", cut.Find(".trip-chat-assistant-message").TextContent));
        Assert.DoesNotContain("<b>Answer</b>", cut.Markup);
        var sourceLink = cut.Find(".trip-chat-citations a");
        Assert.Equal($"/trips/{citation.TripId}", sourceLink.GetAttribute("href"));
        Assert.Contains("Flight to Seattle", sourceLink.TextContent);
        Assert.Equal("When does the first leg start?", api.Request?.Message);
        Assert.Empty(api.Request?.PriorUserMessages ?? []);

        await cut.Find(".trip-chat-panel").TriggerEventAsync("onkeydown", new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll("#trip-data-chat-panel"));
    }

    [Fact]
    public async Task RetryableFailurePreservesTranscriptAndRetryDoesNotDuplicateUserTurn()
    {
        const string epoch = "opaque-epoch-retry";
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(new TripChatSessionStore(JSInterop.JSRuntime));
        Services.AddSingleton<AuthenticationStateProvider>(new ChatAuthenticationStateProvider(true, epoch));
        var api = new StubChatApiClient { FailuresRemaining = 1 };
        Services.AddSingleton<ITripDataChatApiClient>(api);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/tripDataChat.js")
            .Setup<TripChatSessionSnapshot>("load", epoch)
            .SetResult(TripChatSessionSnapshot.Empty);

        var cut = Render<ChatComponent>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".trip-chat-activator")));
        await cut.Find(".trip-chat-activator").ClickAsync();
        cut.Find("#trip-chat-message").Input("Find the Seattle trip");
        await cut.Find("button[type='submit']").ClickAsync();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-chat-retry")));
        Assert.Single(cut.FindAll(".trip-chat-user-message"));
        await cut.Find(".trip-chat-retry").ClickAsync();

        cut.WaitForAssertion(() => Assert.Contains("Recovered answer", cut.Markup));
        Assert.Equal(2, api.CallCount);
        Assert.Single(cut.FindAll(".trip-chat-user-message"));
    }

    [Fact]
    public async Task NewChatClearsTranscriptAndKeepsPaneOpen()
    {
        const string epoch = "opaque-epoch-new-chat";
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(new TripChatSessionStore(JSInterop.JSRuntime));
        Services.AddSingleton<AuthenticationStateProvider>(new ChatAuthenticationStateProvider(true, epoch));
        var api = new StubChatApiClient();
        Services.AddSingleton<ITripDataChatApiClient>(api);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.SetupModule("./js/tripDataChat.js")
            .Setup<TripChatSessionSnapshot>("load", epoch)
            .SetResult(TripChatSessionSnapshot.Empty);

        var cut = Render<ChatComponent>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".trip-chat-activator")));
        await cut.Find(".trip-chat-activator").ClickAsync();
        Assert.True(cut.Find(".trip-chat-new").HasAttribute("disabled"));

        cut.Find("#trip-chat-message").Input("Which trip includes Seattle?");
        await cut.Find("button[type='submit']").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".trip-chat-assistant-message")));

        await cut.Find(".trip-chat-new").ClickAsync();

        Assert.Empty(cut.FindAll(".trip-chat-user-message"));
        Assert.Empty(cut.FindAll(".trip-chat-assistant-message"));
        Assert.Single(cut.FindAll("#trip-data-chat-panel"));
        Assert.Equal(string.Empty, cut.Find("#trip-chat-message").GetAttribute("value") ?? string.Empty);
        Assert.Contains("Started a new chat.", cut.Find(".trip-chat-live-status").TextContent);
    }

    private sealed class StubChatApiClient : ITripDataChatApiClient
    {
        public int CallCount { get; private set; }
        public int FailuresRemaining { get; set; }
        public TripDataChatRequest? Request { get; private set; }
        public TripDataChatResponse Response { get; init; } = new(TripDataChatStatus.InsufficientData, "No data.", []);

        public Task<TripDataChatResponse> AskAsync(TripDataChatRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new TripDataChatApiException(TripDataChatFailureKind.Retryable);
            }
            if (CallCount > 1)
            {
                return Task.FromResult(new TripDataChatResponse(TripDataChatStatus.Answered, "Recovered answer", []));
            }
            return Task.FromResult(Response);
        }
    }

    private sealed class ChatAuthenticationStateProvider(bool authenticated, string? epoch) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = authenticated
                ? new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "test-user"),
                    new Claim(TripChatSessionEpoch.ClaimType, epoch!)
                }, "Test")
                : new ClaimsIdentity();
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}