using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Auth;

public class AuthenticatedApiTokenHandlerTests
{
    [Fact]
    public async Task SendAsync_AttachesBearerAuthorizationHeader()
    {
        var inner = new CapturingHandler();
        var handler = new AuthenticatedApiTokenHandler(new FixedTokenProvider())
        {
            InnerHandler = inner
        };
        using var invoker = new HttpMessageInvoker(handler);

        var response = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "https://api.example.test/api/trips/recent"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(inner.Authorization);
        Assert.Equal("Bearer", inner.Authorization!.Scheme);
        Assert.False(string.IsNullOrWhiteSpace(inner.Authorization.Parameter));
    }

    private sealed class FixedTokenProvider : ITripPlannerApiTokenProvider
    {
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
            => Task.FromResult("test-access-token-value");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

public sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ClaimsPrincipal _principal;

    public TestAuthenticationStateProvider(bool isAuthenticated)
    {
        _principal = isAuthenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") }, "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_principal));
}
