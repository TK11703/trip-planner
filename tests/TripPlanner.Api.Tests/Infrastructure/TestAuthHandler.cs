using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TripPlanner.Api.Tests.Infrastructure;

public sealed class TestAuthHandler : AuthenticationHandler<TestAuthHandlerOptions>
{
    public const string SchemeName = "Test";
    public const string TestUserHeader = "X-Test-User";
    public const string TestNameHeader = "X-Test-Name";
    public const string TestFirstNameHeader = "X-Test-First-Name";
    public const string TestLastNameHeader = "X-Test-Last-Name";
    public const string TestEmailHeader = "X-Test-Email";
    public const string TestScopeHeader = "X-Test-Scope";

    public TestAuthHandler(IOptionsMonitor<TestAuthHandlerOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[TestUserHeader].ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var displayName = Request.Headers[TestNameHeader].ToString();
        var claims = new List<Claim>
        {
            new("http://schemas.microsoft.com/identity/claims/objectidentifier", userId),
            new(ClaimTypes.NameIdentifier, userId),
            new("scp", Request.Headers[TestScopeHeader].FirstOrDefault() ?? "access_as_user api://trip-planner-api/access_as_user"),
        };
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            claims.Add(new Claim("name", displayName));
        }
        var firstName = Request.Headers[TestFirstNameHeader].ToString();
        if (!string.IsNullOrWhiteSpace(firstName))
        {
            claims.Add(new Claim("given_name", firstName));
        }
        var lastName = Request.Headers[TestLastNameHeader].ToString();
        if (!string.IsNullOrWhiteSpace(lastName))
        {
            claims.Add(new Claim("family_name", lastName));
        }
        var email = Request.Headers[TestEmailHeader].ToString();
        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions { }

public static class TestUsers
{
    public const string UserA = "user-a-immutable-id";
    public const string UserB = "user-b-immutable-id";
}
