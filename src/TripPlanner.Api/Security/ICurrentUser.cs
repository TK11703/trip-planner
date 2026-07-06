using System.Security.Claims;

namespace TripPlanner.Api.Security;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string UserId { get; }
    string? FirstName { get; }
    string? LastName { get; }
    string? DisplayName { get; }
    string? Email { get; }
    string? TryGetUserId();
}
