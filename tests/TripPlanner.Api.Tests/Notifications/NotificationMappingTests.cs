using TripPlanner.Api.Features.Notifications;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Tests.Notifications;

public class NotificationMappingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("Flight ABC123 confirmation")]
    public void ResponseIncludesTheRelatedEmailSubject(string? subject)
    {
        var record = new NotificationRecord(
            Guid.NewGuid(), "traveler", NotificationCategories.ParsedEmail,
            NotificationKind.Actionable, NotificationTargetType.Person, null, null,
            "New trip item ready to review", "Review the extracted items.",
            DateTimeOffset.UtcNow, null, NotificationActionStatus.Pending, null, null, null,
            NotificationEmailStatus.NotRequested, subject);

        var response = record.ToResponse(canOpenTrip: false);

        Assert.Equal(subject, response.RelatedEmailSubject);
        Assert.Equal(record.Title, response.Title);
        Assert.Equal(record.Message, response.Message);
        Assert.Equal(record.NotificationId, response.NotificationId);
        Assert.False(response.CanOpenTrip);
    }
}
