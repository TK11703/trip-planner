using TripPlanner.Contracts.Trips;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Security;

/// <summary>
/// Resolves a caller's access to a trip (owner, collaborator, viewer, or none) so endpoints can
/// enforce authorization server-side and reuse the trip owner's id when reading or writing
/// owner-scoped data. All access decisions must go through this resolver rather than trusting the UI.
/// </summary>
public interface ITripAccessResolver
{
    Task<TripAccess?> ResolveAsync(string callerUserId, Guid tripId, CancellationToken ct);
}

public sealed class TripAccessResolver : ITripAccessResolver
{
    private readonly ITripSharingRepository _sharing;
    private readonly ICurrentUser _currentUser;
    public TripAccessResolver(ITripSharingRepository sharing, ICurrentUser currentUser)
    {
        _sharing = sharing;
        _currentUser = currentUser;
    }

    public Task<TripAccess?> ResolveAsync(string callerUserId, Guid tripId, CancellationToken ct)
        => _sharing.GetAccessAsync(callerUserId, _currentUser.Email, tripId, ct);
}

/// <summary>Convenience checks over an access level.</summary>
public static class TripAccessExtensions
{
    public static bool CanRead(this TripAccess access) => true;

    /// <summary>Owner and collaborator can edit itinerary content (legs/events).</summary>
    public static bool CanEditContent(this TripAccess access)
        => access.AccessLevel is TripAccessLevel.Owner or TripAccessLevel.Collaborator;

    /// <summary>Only the owner can edit trip metadata, delete the trip, or manage sharing.</summary>
    public static bool IsOwner(this TripAccess access)
        => access.AccessLevel == TripAccessLevel.Owner;
}
