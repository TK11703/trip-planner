using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using TripPlanner.Web.Features.Authentication;
using Xunit;

namespace TripPlanner.Web.Tests.Auth;

/// <summary>
/// Covers the real consent handler rather than a stand-in, because its contract is surprising:
/// it signals "I cannot fix this" by rethrowing rather than by returning. Exercised through the
/// non-Blazor redirect path, which is the only one reachable in a test — the Blazor path needs a
/// live circuit's NavigationManager.
/// </summary>
public class MicrosoftIdentityApiChallengeHandlerTests
{
    private const string ApiScope = "api://trip-planner/access_as_user";

    [Theory]
    [InlineData(MsalError.InvalidGrantError)]
    [InlineData(MsalError.UserNullError)]
    public void RedirectsWhenReSigningTheUserInWouldFixTheChallenge(string errorCode)
    {
        var context = NewHttpContext();
        var handler = NewHandler(context);

        var handled = handler.TryHandle(NewChallenge(errorCode));

        Assert.True(handled);
        Assert.Equal((int)HttpStatusCode.Redirect, context.Response.StatusCode);
    }

    [Fact]
    public void ReportsUnhandledInsteadOfThrowingWhenReSigningInWouldNotHelp()
    {
        // The consent handler rethrows here. Letting that escape would tear down the circuit,
        // which is worse than the error message it replaces.
        var handler = NewHandler(NewHttpContext());

        var handled = handler.TryHandle(NewChallenge("some_unrelated_error"));

        Assert.False(handled);
    }

    [Fact]
    public void TreatsABareMsalExceptionAsAChallenge()
    {
        // MSAL raises this directly on some paths. It carries the same signal but the consent
        // handler only recognises the wrapper type.
        var context = NewHttpContext();
        var handler = NewHandler(context);

        var handled = handler.TryHandle(new MsalUiRequiredException(MsalError.InvalidGrantError, "interaction required"));

        Assert.True(handled);
        Assert.Equal((int)HttpStatusCode.Redirect, context.Response.StatusCode);
    }

    [Fact]
    public void FindsAChallengeNestedInsideAnotherException()
    {
        var context = NewHttpContext();
        var handler = NewHandler(context);

        var wrapped = new InvalidOperationException("Call failed.", NewChallenge(MsalError.InvalidGrantError));

        Assert.True(handler.TryHandle(wrapped));
        Assert.Equal((int)HttpStatusCode.Redirect, context.Response.StatusCode);
    }

    [Fact]
    public void IgnoresExceptionsThatAreNotChallenges()
    {
        var context = NewHttpContext();
        var handler = NewHandler(context);

        Assert.False(handler.TryHandle(new HttpRequestException("The trip service is unavailable.")));
        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
    }

    private static MicrosoftIdentityWebChallengeUserException NewChallenge(string errorCode) =>
        new(new MsalUiRequiredException(errorCode, "interaction required"), [ApiScope]);

    private static DefaultHttpContext NewHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("trip-planner.example");
        context.Request.Path = "/trips";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("preferred_username", "traveler@example.com")], "TestAuth"));
        return context;
    }

    private static MicrosoftIdentityApiChallengeHandler NewHandler(HttpContext context)
    {
        var services = new ServiceCollection()
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = context })
            .BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureEntra:ApiScopes:0"] = ApiScope })
            .Build();

        // IsBlazorServer stays false so the handler redirects through HttpResponse. A circuit sets
        // it to true in production, which swaps in NavigationManager but leaves this logic alone.
        return new MicrosoftIdentityApiChallengeHandler(
            new MicrosoftIdentityConsentAndConditionalAccessHandler(services),
            configuration);
    }
}
