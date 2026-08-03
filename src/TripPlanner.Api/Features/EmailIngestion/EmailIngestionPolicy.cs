using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Authorization for the relay ingestion endpoint.
///
/// The external automation relay (a Logic App) calls the API with its own managed identity,
/// so the caller is an application, not a traveler. Being authenticated is not enough: any
/// token issued by the tenant would otherwise be able to submit mail on any traveler's behalf.
/// The relay must therefore present the <c>EmailIngestion.Relay</c> application role, which is
/// granted to exactly one service principal on the API app registration.
///
/// The caller identity is used only for authorization. The owning traveler is always resolved
/// from the message sender.
/// </summary>
public static class EmailIngestionPolicy
{
    /// <summary>Policy name used in <c>RequireAuthorization</c> calls.</summary>
    public const string RelayPolicy = "EmailIngestionRelay";

    /// <summary>The application role a relay must carry to submit messages.</summary>
    public const string RelayAppRole = "EmailIngestion.Relay";

    /// <summary>Registers the relay policy on the supplied <see cref="AuthorizationOptions"/>.</summary>
    public static void AddEmailIngestionPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RelayPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => HasRelayAppRole(context.User));
        });
    }

    private static bool HasRelayAppRole(ClaimsPrincipal user)
    {
        // Entra emits application roles in "roles"; the JWT handler may also map them to the
        // standard role claim type. A space-delimited value is accepted defensively.
        foreach (var claim in user.FindAll("roles").Concat(user.FindAll(ClaimTypes.Role)))
        {
            var values = claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Any(value => string.Equals(value, RelayAppRole, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
