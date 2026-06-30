using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TripPlanner.Api.Security;

public sealed class CurrentUser : ICurrentUser
{
    private const string EntraObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string OidClaim = "oid";

    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string UserId => TryGetUserId()
        ?? throw new InvalidOperationException("Current user is not authenticated.");

    public string? DisplayName => Principal?.FindFirst("name")?.Value ?? Principal?.Identity?.Name;
    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value
        ?? Principal?.FindFirst("preferred_username")?.Value
        ?? Principal?.FindFirst("upn")?.Value;

    public string? TryGetUserId()
    {
        var p = Principal;
        if (p?.Identity?.IsAuthenticated != true) return null;
        var value = p.FindFirst(EntraObjectIdClaim)?.Value
            ?? p.FindFirst(OidClaim)?.Value
            ?? p.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? p.FindFirst("sub")?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
