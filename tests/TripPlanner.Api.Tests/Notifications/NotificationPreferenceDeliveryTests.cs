using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Tests.Notifications;

public class NotificationPreferenceDeliveryTests
{
    [Fact]
    public async Task CreateAsync_SuppressesEverything_WhenCategoryDisabled()
    {
        var repository = new FakeNotificationRepository();
        repository.SetPreference("user-1", NotificationCategories.ItineraryChanges, inAppEnabled: false, emailEnabled: false);
        var email = new FakeEmailSender();
        var service = new NotificationService(repository, email);

        var result = await service.CreateAsync(NewItineraryNotification("user-1"), CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(repository.Created);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task CreateAsync_DeliversInApp_ButNotEmail_WhenEmailDisabled()
    {
        var repository = new FakeNotificationRepository();
        repository.SetPreference("user-1", NotificationCategories.ItineraryChanges, inAppEnabled: true, emailEnabled: false);
        var email = new FakeEmailSender();
        var service = new NotificationService(repository, email);

        var result = await service.CreateAsync(NewItineraryNotification("user-1"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(repository.Created);
        Assert.Empty(email.Sent);
    }

    [Fact]
    public async Task CreateAsync_DeliversBoth_WhenBothChannelsEnabled()
    {
        var repository = new FakeNotificationRepository();
        repository.SetPreference("user-1", NotificationCategories.ItineraryChanges, inAppEnabled: true, emailEnabled: true);
        var email = new FakeEmailSender();
        var service = new NotificationService(repository, email);

        var result = await service.CreateAsync(NewItineraryNotification("user-1", "user-1@example.test"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(repository.Created);
        Assert.Single(email.Sent);
    }

    [Fact]
    public async Task CreateAsync_UsesCategoryDefaults_WhenNoPreferenceSaved()
    {
        var repository = new FakeNotificationRepository();
        var email = new FakeEmailSender();
        var service = new NotificationService(repository, email);

        var result = await service.CreateAsync(NewItineraryNotification("user-1", "user-1@example.test"), CancellationToken.None);

        // Defaults for ItineraryChanges are in-app + email enabled.
        Assert.NotNull(result);
        Assert.Single(repository.Created);
        Assert.Single(email.Sent);
    }

    private static NewNotification NewItineraryNotification(string recipient, string? email = null)
        => new(
            RecipientUserId: recipient,
            Category: NotificationCategories.ItineraryChanges,
            Kind: NotificationKind.Awareness,
            TargetType: NotificationTargetType.Trip,
            RelatedTripId: Guid.NewGuid(),
            Title: "A shared trip changed",
            Message: "Someone updated the trip details.",
            SourceEventKey: $"itinerary-change:{Guid.NewGuid():N}",
            RecipientEmail: email);
}
