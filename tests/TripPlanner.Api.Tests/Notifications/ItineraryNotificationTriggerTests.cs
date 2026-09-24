using System.Globalization;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Contracts.Profile;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.TripSharing;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Tests.Notifications;

public class ItineraryNotificationTriggerTests
{
    private static readonly Guid TripId = Guid.NewGuid();

    [Fact]
    public async Task NotifyChangeAsync_NotifiesViewersAndOwner_ButNotActor()
    {
        var notifications = new RecordingNotificationService();
        var sharing = new FakeTripSharingRepository(
            new TripShareMember("viewer-1", "Viewer One", "v1@example.test", TripAccessLevel.Viewer, DateTimeOffset.UtcNow),
            new TripShareMember("editor-1", "Editor One", "e1@example.test", TripAccessLevel.Collaborator, DateTimeOffset.UtcNow));
        var profiles = new FakeProfileRepository("owner-1", "owner@example.test");
        var service = new ItineraryNotificationService(notifications, sharing, profiles, new SystemClock());

        // The owner performs the change; the owner must not be notified, but viewers/editors are.
        await service.NotifyChangeAsync(TripId, ownerUserId: "owner-1", actorUserId: "owner-1", actorDisplayName: "Owner One", ItineraryChangeKind.TripLegCreated, Guid.NewGuid(), CancellationToken.None);

        var recipients = notifications.Created.Select(n => n.RecipientUserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("viewer-1", recipients);
        Assert.Contains("editor-1", recipients);
        Assert.DoesNotContain("owner-1", recipients);
        Assert.All(notifications.Created, n => Assert.Equal(NotificationCategories.ItineraryChanges, n.Category));
    }

    [Fact]
    public async Task NotifyChangeAsync_ExcludesActor_WhenActorIsAViewer()
    {
        var notifications = new RecordingNotificationService();
        var sharing = new FakeTripSharingRepository(
            new TripShareMember("viewer-1", "Viewer One", "v1@example.test", TripAccessLevel.Viewer, DateTimeOffset.UtcNow),
            new TripShareMember("editor-1", "Editor One", "e1@example.test", TripAccessLevel.Collaborator, DateTimeOffset.UtcNow));
        var profiles = new FakeProfileRepository("owner-1", "owner@example.test");
        var service = new ItineraryNotificationService(notifications, sharing, profiles, new SystemClock());

        // A collaborator performs the change; they are excluded, owner and the other viewer are notified.
        await service.NotifyChangeAsync(TripId, ownerUserId: "owner-1", actorUserId: "editor-1", actorDisplayName: "Editor One", ItineraryChangeKind.TripItemUpdated, Guid.NewGuid(), CancellationToken.None);

        var recipients = notifications.Created.Select(n => n.RecipientUserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("owner-1", recipients);
        Assert.Contains("viewer-1", recipients);
        Assert.DoesNotContain("editor-1", recipients);
    }

    [Fact]
    public async Task NotifyChangeAsync_ReusesSourceEventKey_ForTheSameEntityWithinTheWindow()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-09-24T10:00:05Z", CultureInfo.InvariantCulture));
        var service = BuildService(out var notifications, clock);
        var itemId = Guid.NewGuid();

        // A resent request repeats the same change; both attempts must land on one key so the
        // stored unique index suppresses the second notification.
        await service.NotifyChangeAsync(TripId, "owner-1", "editor-1", "Editor One", ItineraryChangeKind.TripItemUpdated, itemId, CancellationToken.None);
        clock.UtcNow = DateTimeOffset.Parse("2026-09-24T10:00:47Z", CultureInfo.InvariantCulture);
        await service.NotifyChangeAsync(TripId, "owner-1", "editor-1", "Editor One", ItineraryChangeKind.TripItemUpdated, itemId, CancellationToken.None);

        var keysByRecipient = notifications.Created
            .GroupBy(n => n.RecipientUserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(n => n.SourceEventKey).Distinct().Count(), StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(keysByRecipient);
        Assert.All(keysByRecipient.Values, distinct => Assert.Equal(1, distinct));
    }

    [Fact]
    public async Task NotifyChangeAsync_UsesDistinctSourceEventKeys_ForDifferentEntities()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-09-24T10:00:05Z", CultureInfo.InvariantCulture));
        var service = BuildService(out var notifications, clock);

        await service.NotifyChangeAsync(TripId, "owner-1", "editor-1", "Editor One", ItineraryChangeKind.TripItemUpdated, Guid.NewGuid(), CancellationToken.None);
        await service.NotifyChangeAsync(TripId, "owner-1", "editor-1", "Editor One", ItineraryChangeKind.TripItemUpdated, Guid.NewGuid(), CancellationToken.None);

        var perRecipient = notifications.Created
            .GroupBy(n => n.RecipientUserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(n => n.SourceEventKey).Distinct().Count(), StringComparer.OrdinalIgnoreCase);
        Assert.All(perRecipient.Values, distinct => Assert.Equal(2, distinct));
    }

    [Fact]
    public async Task NotifyChangeAsync_UsesDistinctSourceEventKeys_ForTheSameEntityInALaterWindow()
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-09-24T10:00:05Z", CultureInfo.InvariantCulture));
        var service = BuildService(out var notifications, clock);
        var itemId = Guid.NewGuid();

        await service.NotifyChangeAsync(TripId, "owner-1", "editor-1", "Editor One", ItineraryChangeKind.TripItemUpdated, itemId, CancellationToken.None);
        clock.UtcNow = DateTimeOffset.Parse("2026-09-24T10:05:05Z", CultureInfo.InvariantCulture);
        await service.NotifyChangeAsync(TripId, "owner-1", "editor-1", "Editor One", ItineraryChangeKind.TripItemUpdated, itemId, CancellationToken.None);

        var perRecipient = notifications.Created
            .GroupBy(n => n.RecipientUserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(n => n.SourceEventKey).Distinct().Count(), StringComparer.OrdinalIgnoreCase);
        Assert.All(perRecipient.Values, distinct => Assert.Equal(2, distinct));
    }

    private static ItineraryNotificationService BuildService(out RecordingNotificationService notifications, IClock clock)
    {
        notifications = new RecordingNotificationService();
        var sharing = new FakeTripSharingRepository(
            new TripShareMember("viewer-1", "Viewer One", "v1@example.test", TripAccessLevel.Viewer, DateTimeOffset.UtcNow),
            new TripShareMember("editor-1", "Editor One", "e1@example.test", TripAccessLevel.Collaborator, DateTimeOffset.UtcNow));
        return new ItineraryNotificationService(notifications, sharing, new FakeProfileRepository("owner-1", "owner@example.test"), clock);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<NewNotification> Created { get; } = new();

        public Task<NotificationRecord?> CreateAsync(NewNotification notification, CancellationToken ct)
        {
            Created.Add(notification);
            return Task.FromResult<NotificationRecord?>(null);
        }
    }

    private sealed class FakeTripSharingRepository : ITripSharingRepository
    {
        private readonly IReadOnlyList<TripShareMember> _members;
        public FakeTripSharingRepository(params TripShareMember[] members) => _members = members;

        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct) => Task.FromResult(_members);

        public Task<TripAccess?> GetAccessAsync(string userId, string? email, Guid tripId, CancellationToken ct) => Task.FromResult<TripAccess?>(null);
        public Task<TripShareMember?> UpsertShareAsync(string ownerUserId, Guid tripId, UpsertTripShareRequest request, DateTimeOffset nowUtc, CancellationToken ct) => Task.FromResult<TripShareMember?>(null);
        public Task<TripShareMember?> UpdateAccessAsync(string ownerUserId, Guid tripId, string memberUserId, TripAccessLevel accessLevel, DateTimeOffset nowUtc, CancellationToken ct) => Task.FromResult<TripShareMember?>(null);
        public Task<int> DeleteShareAsync(string ownerUserId, Guid tripId, string memberUserId, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class FakeProfileRepository : IUserProfileRepository
    {
        private readonly string _ownerId;
        private readonly string _ownerEmail;
        public FakeProfileRepository(string ownerId, string ownerEmail) { _ownerId = ownerId; _ownerEmail = ownerEmail; }

        public Task<UserProfileResponse?> GetAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(userId, _ownerId, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<UserProfileResponse?>(null);
            }

            var profile = new UserProfileResponse(
                _ownerId, "Owner", "One", "Owner One", _ownerEmail, "UTC", MapProviders.Bing, true,
                NotificationPreferences.Default, new PersonalizationPreferences(null, null, null, null),
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return Task.FromResult<UserProfileResponse?>(profile);
        }

        public Task<UserProfileResponse> EnsureFromAuthenticatedUserAsync(string userId, string? firstName, string? lastName, string? displayName, string? email, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<string>> FindUserIdsByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> matches = string.Equals(email.Trim(), _ownerEmail, StringComparison.OrdinalIgnoreCase)
                ? new[] { _ownerId }
                : Array.Empty<string>();
            return Task.FromResult(matches);
        }

        public Task<UserProfileResponse?> UpdateAsync(string userId, UpdateUserProfileRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
