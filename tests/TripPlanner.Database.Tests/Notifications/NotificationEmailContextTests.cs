using Dapper;
using Npgsql;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.Tests.Infrastructure;

namespace TripPlanner.Database.Tests.Notifications;

[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class NotificationEmailContextTests
{
    private readonly PostgresFixture _fixture;
    private readonly NotificationRepository _repository;

    public NotificationEmailContextTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _repository = new NotificationRepository(
            new StubConnectionFactory(fixture.ConnectionString), fixture.Sql);
    }

    [Theory]
    [InlineData("Flight ABC123 confirmation")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<b>Hotel & flight</b>")]
    public async Task ExistingNotificationsReadTheirSourceEmailSubjectEvenAfterCompletion(string subject)
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var emailId = await SeedEmailAsync(userId, subject);
        var notification = await CreateAsync(userId, $"email-parsed:{emailId}");

        Assert.NotNull(notification);
        var listed = Assert.Single(await _repository.GetListAsync(userId, 20));
        var fetched = await _repository.GetAsync(userId, notification.NotificationId);
        Assert.Equal(subject, listed.RelatedEmailSubject);
        Assert.NotNull(fetched);
        Assert.Equal(subject, fetched.RelatedEmailSubject);

        var completed = await _repository.CompleteAsync(
            userId, notification.NotificationId, userId, "Traveler", DateTimeOffset.UtcNow);
        Assert.NotNull(completed);
        Assert.Equal(NotificationActionStatus.Completed, completed.ActionStatus);
        Assert.Equal(subject, Assert.Single(await _repository.GetListAsync(userId, 20)).RelatedEmailSubject);
    }

    [Fact]
    public async Task SourceEmailMustBelongToTheNotificationRecipient()
    {
        var ownerId = $"owner-{Guid.NewGuid():N}";
        var otherUserId = $"other-{Guid.NewGuid():N}";
        var emailId = await SeedEmailAsync(ownerId, "Private booking");
        var notification = await CreateAsync(otherUserId, $"email-parsed:{emailId}");

        Assert.NotNull(notification);
        Assert.Null(Assert.Single(await _repository.GetListAsync(otherUserId, 20)).RelatedEmailSubject);
        Assert.Null((await _repository.GetAsync(otherUserId, notification.NotificationId))!.RelatedEmailSubject);
        Assert.Null(await _repository.GetAsync(ownerId, notification.NotificationId));
        Assert.Empty(await _repository.GetListAsync(ownerId, 20));
    }

    [Theory]
    [InlineData(NotificationCategories.ParsedEmail, "email-parsed:not-a-guid")]
    [InlineData(NotificationCategories.ParsedEmail, "email-parsed:00000000-0000-0000-0000-000000000000")]
    [InlineData(NotificationCategories.ItineraryChanges, null)]
    public async Task UnrelatedOrUnavailableEmailsLeaveTheNotificationUnchanged(string category, string? sourceKey)
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var emailId = await SeedEmailAsync(userId, "Unrelated booking");
        var notification = await CreateAsync(userId, sourceKey ?? $"email-parsed:{emailId}", category);

        Assert.NotNull(notification);
        var listed = Assert.Single(await _repository.GetListAsync(userId, 20));
        var fetched = await _repository.GetAsync(userId, notification.NotificationId);
        Assert.NotNull(fetched);
        Assert.Null(listed.RelatedEmailSubject);
        Assert.Null(fetched.RelatedEmailSubject);
        Assert.Equal(notification.Title, listed.Title);
        Assert.Equal(notification.Message, listed.Message);
    }

    private Task<NotificationRecord?> CreateAsync(
        string userId, string sourceKey, string category = NotificationCategories.ParsedEmail)
        => _repository.CreateAsync(new NewNotification(
            userId, category, NotificationKind.Actionable, NotificationTargetType.Person, null,
            "New trip item ready to review", "Review and confirm the extracted items.", sourceKey),
            DateTimeOffset.UtcNow);

    private async Task<Guid> SeedEmailAsync(string userId, string subject)
    {
        var emailId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            INSERT INTO inbox_emails (inbox_email_id, user_id, sender, subject, received_at, parse_status, dedupe_hash)
            VALUES (@emailId, @userId, 'sender@example.com', @subject, now(), 'parsed', @hash);
            """,
            new { emailId, userId, subject, hash = Guid.NewGuid().ToString() });
        return emailId;
    }
}
