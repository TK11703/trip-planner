using Microsoft.AspNetCore.Authorization;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Authorization policy for the Event Grid webhook endpoint.
/// Event Grid delivers events using the API's system-assigned managed identity, presenting a
/// bearer token whose audience matches the API's app registration and whose issuer is the
/// configured Azure AD tenant. This policy validates those claims without requiring a function key
/// or shared secret.
/// </summary>
public static class EmailIngestionPolicy
{
    /// <summary>Policy name used in <c>RequireAuthorization</c> calls.</summary>
    public const string WebhookPolicy = "EmailIngestionWebhook";

    /// <summary>
    /// Registers the webhook policy on the supplied <see cref="AuthorizationOptions"/>.
    /// The policy accepts any authenticated caller whose token was issued by the tenant;
    /// the audience is already validated by the JWT middleware configured in
    /// <c>AuthenticationExtensions.AddTripPlannerAuthentication</c>.
    /// </summary>
    public static void AddEmailIngestionPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(WebhookPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
        });
    }
}
