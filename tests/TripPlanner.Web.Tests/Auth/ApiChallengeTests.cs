using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Authentication;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Web.Tests.Auth;

/// <summary>
/// The token cache is per-process while the auth cookie outlives the process, so a deployment or a
/// scale-to-zero leaves a signed-in user with no access token. Entra answers that with a challenge,
/// which is a redirect instruction rather than a failure worth showing anyone.
/// </summary>
public class ApiChallengeTests : TestContext
{
    [Fact]
    public void ChallengeIsHandedToTheChallengeHandlerRatherThanRendered()
    {
        var challenge = NewChallenge();
        var handler = new FakeChallengeHandler(handles: true);
        Arrange(challenge, handler);

        var cut = RenderComponent<RecentTripsList>();

        cut.WaitForAssertion(() => Assert.Same(challenge, handler.Received));
        Assert.DoesNotContain("IDW10502", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("couldn't load your trips", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void GenuineFailureIsStillRendered()
    {
        var handler = new FakeChallengeHandler(handles: false);
        Arrange(new HttpRequestException("The trip service is unavailable."), handler);

        var cut = RenderComponent<RecentTripsList>();

        cut.WaitForAssertion(() => Assert.Contains("The trip service is unavailable.", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void FailureIsRenderedWhenNoChallengeHandlerIsRegistered()
    {
        // The handler is resolved optionally, so a missing registration must degrade to the old
        // behaviour rather than swallowing the error and rendering an empty panel.
        Arrange(new HttpRequestException("The trip service is unavailable."), handler: null);

        var cut = RenderComponent<RecentTripsList>();

        cut.WaitForAssertion(() => Assert.Contains("The trip service is unavailable.", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void CancellationIsNotRenderedAsAnError()
    {
        Arrange(new OperationCanceledException(), new FakeChallengeHandler(handles: false));

        var cut = RenderComponent<RecentTripsList>();

        cut.WaitForAssertion(() => Assert.DoesNotContain("couldn't load your trips", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void TripDetailsChallengeStaysLoadingInsteadOfClaimingTheTripIsMissing()
    {
        // Returning to a tab whose circuit was resumed on a process that lost its token cache must
        // hand off to sign-in, not tell the traveler their trip does not exist.
        var challenge = NewChallenge();
        var handler = new FakeChallengeHandler(handles: true);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient { DetailFailure = challenge });
        Services.AddSingleton<IApiChallengeHandler>(handler);

        var cut = RenderComponent<TripDetails>(p => p.Add(x => x.TripId, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.Same(challenge, handler.Received));
        Assert.DoesNotContain("isn't available", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Loading trip", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TripDetailsGenuineFailureStillShowsNotAvailable()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient { DetailFailure = new HttpRequestException("down") });
        Services.AddSingleton<IApiChallengeHandler>(new FakeChallengeHandler(handles: false));

        var cut = RenderComponent<TripDetails>(p => p.Add(x => x.TripId, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.Contains("isn't available", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void TripPrintChallengeIsHandledRatherThanThrown()
    {
        var challenge = NewChallenge();
        var handler = new FakeChallengeHandler(handles: true);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient { DetailFailure = challenge });
        Services.AddSingleton<IApiChallengeHandler>(handler);

        var cut = RenderComponent<TripPrint>(p => p.Add(x => x.TripId, Guid.NewGuid()));

        cut.WaitForAssertion(() => Assert.Same(challenge, handler.Received));
        Assert.DoesNotContain("isn't available", cut.Markup, StringComparison.Ordinal);
    }

    private void Arrange(Exception failure, FakeChallengeHandler? handler)
    {
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient { RecentFailure = failure });
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));

        if (handler is not null)
        {
            Services.AddSingleton<IApiChallengeHandler>(handler);
        }
    }

    private static MicrosoftIdentityWebChallengeUserException NewChallenge() =>
        new(new MsalUiRequiredException("invalid_grant", "AADSTS50079: interaction required."), ["api://trip-planner/access_as_user"]);

    private sealed class FakeChallengeHandler : IApiChallengeHandler
    {
        private readonly bool _handles;

        public FakeChallengeHandler(bool handles) => _handles = handles;

        public Exception? Received { get; private set; }

        public bool TryHandle(Exception exception)
        {
            Received = exception;
            return _handles;
        }
    }
}
