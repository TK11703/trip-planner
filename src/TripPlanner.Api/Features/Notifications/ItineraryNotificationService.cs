using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.TripSharing;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Features.Notifications;

/// <summary>The kind of itinerary change that occurred, used to compose the recipient's message.</summary>
public enum ItineraryChangeKind
{
    TripUpdated,
    TripLegCreated,
    TripLegUpdated,
    TripLegDeleted,
    TripEventCreated,
    TripEventUpdated,
    TripEventDeleted
}

/// <summary>
/// Generates itinerary-change notifications for everyone with current view or edit access to a trip,
/// excluding the person who performed the change. Each recipient's preferences are applied by the
/// underlying <see cref="INotificationService"/> before any delivery.
/// </summary>
public interface IItineraryNotificationService
{
    Task NotifyChangeAsync(
        Guid tripId,
        string ownerUserId,
        string actorUserId,
        string? actorDisplayName,
        ItineraryChangeKind change,
        CancellationToken ct);
}

public sealed class ItineraryNotificationService : IItineraryNotificationService
{
    private readonly INotificationService _notifications;
    private readonly ITripSharingRepository _sharing;
    private readonly IUserProfileRepository _profiles;

    public ItineraryNotificationService(
        INotificationService notifications,
        ITripSharingRepository sharing,
        IUserProfileRepository profiles)
    {
        _notifications = notifications;
        _sharing = sharing;
        _profiles = profiles;
    }

    public async Task NotifyChangeAsync(
        Guid tripId,
        string ownerUserId,
        string actorUserId,
        string? actorDisplayName,
        ItineraryChangeKind change,
        CancellationToken ct)
    {
        // Failure to notify must never fail the underlying itinerary change.
        try
        {
            var recipients = await ResolveRecipientsAsync(tripId, ownerUserId, actorUserId, ct);
            if (recipients.Count == 0)
            {
                return;
            }

            var actorName = string.IsNullOrWhiteSpace(actorDisplayName) ? "Someone" : actorDisplayName;
            var title = "A shared trip changed";
            var message = $"{actorName} {DescribeChange(change)}.";
            var eventToken = Guid.NewGuid().ToString("N");

            foreach (var recipient in recipients)
            {
                await _notifications.CreateAsync(new NewNotification(
                    RecipientUserId: recipient.UserId,
                    Category: NotificationCategories.ItineraryChanges,
                    Kind: NotificationKind.Awareness,
                    TargetType: NotificationTargetType.Trip,
                    RelatedTripId: tripId,
                    Title: title,
                    Message: message,
                    SourceEventKey: $"itinerary-change:{change}:{tripId}:{recipient.UserId}:{eventToken}",
                    RecipientEmail: recipient.Email), ct);
            }
        }
        catch
        {
            // Swallow notification failures; the itinerary change is the primary outcome.
        }
    }

    private async Task<IReadOnlyList<Recipient>> ResolveRecipientsAsync(Guid tripId, string ownerUserId, string actorUserId, CancellationToken ct)
    {
        var recipients = new Dictionary<string, Recipient>(StringComparer.OrdinalIgnoreCase);

        // The owner has full access and should be notified when they are not the actor.
        if (!string.Equals(ownerUserId, actorUserId, StringComparison.OrdinalIgnoreCase))
        {
            var ownerProfile = await _profiles.GetAsync(ownerUserId, ct);
            recipients[ownerUserId] = new Recipient(ownerUserId, ownerProfile?.Email);
        }

        // Everyone the trip is shared with (viewers and collaborators) is a candidate recipient.
        var shares = await _sharing.GetSharesAsync(tripId, ct);
        foreach (var member in shares)
        {
            if (string.Equals(member.UserId, actorUserId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            recipients[member.UserId] = new Recipient(member.UserId, member.Email);
        }

        return recipients.Values.ToArray();
    }

    private static string DescribeChange(ItineraryChangeKind change) => change switch
    {
        ItineraryChangeKind.TripUpdated => "updated the trip details",
        ItineraryChangeKind.TripLegCreated => "added a new leg to the trip",
        ItineraryChangeKind.TripLegUpdated => "updated a leg on the trip",
        ItineraryChangeKind.TripLegDeleted => "removed a leg from the trip",
        ItineraryChangeKind.TripEventCreated => "added a new event to the trip",
        ItineraryChangeKind.TripEventUpdated => "updated an event on the trip",
        ItineraryChangeKind.TripEventDeleted => "removed an event from the trip",
        _ => "changed the trip"
    };

    private sealed record Recipient(string UserId, string? Email);
}
