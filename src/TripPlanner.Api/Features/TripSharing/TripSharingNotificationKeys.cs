namespace TripPlanner.Api.Features.TripSharing;

/// <summary>
/// Builds duplicate-suppression source event keys for trip-sharing notifications. Keys are unique per
/// event occurrence so each successful sharing change produces a fresh notification for the affected
/// person, while the (recipient, key) uniqueness prevents accidental double delivery of one event.
/// </summary>
public static class TripSharingNotificationKeys
{
    public static string Added(Guid tripId, string affectedUserId)
        => $"trip-share-added:{tripId}:{affectedUserId}:{Guid.NewGuid():N}";

    public static string PermissionChanged(Guid tripId, string affectedUserId)
        => $"trip-share-changed:{tripId}:{affectedUserId}:{Guid.NewGuid():N}";

    public static string Removed(Guid tripId, string affectedUserId)
        => $"trip-share-removed:{tripId}:{affectedUserId}:{Guid.NewGuid():N}";
}
