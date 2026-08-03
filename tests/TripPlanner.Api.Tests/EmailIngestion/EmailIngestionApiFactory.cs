using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// Hosts the API with in-memory email-ingestion storage, a scripted recognizer, and a seeded
/// traveler profile, so the relay endpoint can be exercised end to end without a database or a
/// live recognition provider.
/// </summary>
internal sealed class EmailIngestionApiFactory : TestApiFactory
{
    public const string TravelerUserId = "traveler-immutable-id";
    public const string TravelerEmail = "traveler@contoso.com";

    public InMemoryInboxEmailRepository Emails { get; } = new();
    public InMemoryEmailAttachmentRepository Attachments { get; } = new();
    public InMemoryParsedItemDraftRepository Drafts { get; } = new();
    public StubItemRecognizer Recognizer { get; } = new();
    public RecordingNotificationService Notifications { get; } = new();
    public StubProfileDirectory Profiles { get; } = new(TravelerUserId, TravelerEmail);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInboxEmailRepository>();
            services.RemoveAll<IEmailAttachmentRepository>();
            services.RemoveAll<IParsedItemDraftRepository>();
            services.RemoveAll<IItemRecognizer>();
            services.RemoveAll<INotificationService>();
            services.RemoveAll<IUserProfileRepository>();

            services.AddSingleton<IInboxEmailRepository>(Emails);
            services.AddSingleton<IEmailAttachmentRepository>(Attachments);
            services.AddSingleton<IParsedItemDraftRepository>(Drafts);
            services.AddSingleton<IItemRecognizer>(Recognizer);
            services.AddSingleton<INotificationService>(Notifications);
            services.AddSingleton<IUserProfileRepository>(Profiles);
        });
    }

    /// <summary>Builds a valid relay payload; individual tests override just the field under test.</summary>
    public static IngestRelayMessageRequest SampleMessage(
        string? messageId = "message-0001",
        string sender = TravelerEmail,
        string subject = "Your flight confirmation ABC123",
        string? bodyText = "Confirmation ABC123 departs 12 Aug 2026 at 09:30 from SEA.",
        string? bodyHtml = null,
        DateTimeOffset? receivedAt = null,
        IReadOnlyList<RelayEmailAttachment>? attachments = null)
        => new(
            messageId,
            sender,
            "trips@contoso.com",
            subject,
            receivedAt ?? new DateTimeOffset(2026, 7, 31, 14, 5, 0, TimeSpan.Zero),
            bodyText,
            bodyHtml,
            attachments);

    public static RelayEmailAttachment TextAttachment(string fileName, string contentType, string content)
        => new(fileName, contentType, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content)));
}

internal sealed class InMemoryInboxEmailRepository : IInboxEmailRepository
{
    private readonly List<InboxEmailRecord> _rows = [];

    public IReadOnlyList<InboxEmailRecord> Rows
    {
        get { lock (_rows) { return _rows.ToArray(); } }
    }

    public Task<InboxEmailRecord?> InsertAsync(NewInboxEmail email, CancellationToken ct = default)
    {
        lock (_rows)
        {
            if (_rows.Any(r => r.UserId == email.UserId && r.DedupeHash == email.DedupeHash))
            {
                return Task.FromResult<InboxEmailRecord?>(null);
            }

            var record = new InboxEmailRecord(
                Guid.NewGuid(), email.UserId, email.MessageId, email.Sender, email.Recipient, email.Subject,
                email.BodyText, email.BodyHtml, email.ReceivedAt, email.DedupeHash, email.ParseStatus, DateTimeOffset.UtcNow);
            _rows.Add(record);
            return Task.FromResult<InboxEmailRecord?>(record);
        }
    }

    public Task<InboxEmailRecord?> GetByIdAsync(Guid inboxEmailId, string userId, CancellationToken ct = default)
    {
        lock (_rows)
        {
            return Task.FromResult(_rows.FirstOrDefault(r => r.InboxEmailId == inboxEmailId && r.UserId == userId));
        }
    }

    public Task<IReadOnlyList<InboxEmailRecord>> GetListAsync(string userId, int limit, CancellationToken ct = default)
    {
        lock (_rows)
        {
            IReadOnlyList<InboxEmailRecord> result = _rows.Where(r => r.UserId == userId).Take(limit).ToArray();
            return Task.FromResult(result);
        }
    }

    public Task UpdateParseStatusAsync(Guid inboxEmailId, string userId, string parseStatus, CancellationToken ct = default)
    {
        lock (_rows)
        {
            var index = _rows.FindIndex(r => r.InboxEmailId == inboxEmailId && r.UserId == userId);
            if (index >= 0)
            {
                _rows[index] = _rows[index] with { ParseStatus = parseStatus };
            }
        }

        return Task.CompletedTask;
    }
}

internal sealed class InMemoryEmailAttachmentRepository : IEmailAttachmentRepository
{
    private readonly List<(EmailAttachmentRecord Record, byte[] Content)> _rows = [];

    public IReadOnlyList<EmailAttachmentRecord> Rows
    {
        get { lock (_rows) { return _rows.Select(r => r.Record).ToArray(); } }
    }

    public byte[] ContentOf(Guid attachmentId)
    {
        lock (_rows) { return _rows.Single(r => r.Record.AttachmentId == attachmentId).Content; }
    }

    public Task<EmailAttachmentRecord> InsertAsync(NewEmailAttachment attachment, CancellationToken ct = default)
    {
        var record = new EmailAttachmentRecord(
            Guid.NewGuid(), attachment.InboxEmailId, attachment.FileName, attachment.ContentType,
            attachment.Content.Length, attachment.ExtractedText, DateTimeOffset.UtcNow);
        lock (_rows) { _rows.Add((record, attachment.Content)); }
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<EmailAttachmentRecord>> GetForEmailAsync(Guid inboxEmailId, CancellationToken ct = default)
    {
        lock (_rows)
        {
            IReadOnlyList<EmailAttachmentRecord> result = _rows.Where(r => r.Record.InboxEmailId == inboxEmailId).Select(r => r.Record).ToArray();
            return Task.FromResult(result);
        }
    }
}

internal sealed class InMemoryParsedItemDraftRepository : IParsedItemDraftRepository
{
    private readonly List<ParsedItemDraftRecord> _rows = [];

    public IReadOnlyList<ParsedItemDraftRecord> Rows
    {
        get { lock (_rows) { return _rows.ToArray(); } }
    }

    public Task<ParsedItemDraftRecord?> InsertAsync(NewParsedItemDraft draft, CancellationToken ct = default)
    {
        var record = new ParsedItemDraftRecord(
            Guid.NewGuid(), draft.InboxEmailId, draft.UserId, draft.TripId, draft.TripLegId, draft.ItemType,
            draft.Title, draft.Location, draft.StartLocal, draft.StartTimeZoneId, draft.EndLocal, draft.EndTimeZoneId,
            draft.ConfirmationCode, draft.Notes, draft.Confidence, "pending_review", DateTimeOffset.UtcNow);
        lock (_rows) { _rows.Add(record); }
        return Task.FromResult<ParsedItemDraftRecord?>(record);
    }

    public Task<IReadOnlyList<ParsedItemDraftRecord>> GetPendingAsync(string userId, CancellationToken ct = default)
    {
        lock (_rows)
        {
            IReadOnlyList<ParsedItemDraftRecord> result = _rows.Where(r => r.UserId == userId && r.ReviewStatus == "pending_review").ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<ParsedItemDraftRecord?> GetByIdAsync(Guid parsedItemDraftId, string userId, CancellationToken ct = default)
    {
        lock (_rows)
        {
            return Task.FromResult(_rows.FirstOrDefault(r => r.ParsedItemDraftId == parsedItemDraftId && r.UserId == userId));
        }
    }

    public Task<ParsedItemDraftRecord?> UpdateAsync(Guid parsedItemDraftId, string userId, DraftUpdate update, CancellationToken ct = default)
    {
        lock (_rows)
        {
            var index = _rows.FindIndex(r => r.ParsedItemDraftId == parsedItemDraftId && r.UserId == userId);
            if (index < 0)
            {
                return Task.FromResult<ParsedItemDraftRecord?>(null);
            }

            _rows[index] = _rows[index] with
            {
                TripId = update.TripId,
                TripLegId = update.TripLegId,
                ItemType = update.ItemType,
                Title = update.Title,
                Location = update.Location,
                StartLocal = update.StartLocal,
                StartTimeZoneId = update.StartTimeZoneId,
                EndLocal = update.EndLocal,
                EndTimeZoneId = update.EndTimeZoneId,
                ConfirmationCode = update.ConfirmationCode,
                Notes = update.Notes
            };
            return Task.FromResult<ParsedItemDraftRecord?>(_rows[index]);
        }
    }

    public Task<bool> SetReviewStatusAsync(Guid parsedItemDraftId, string userId, string reviewStatus, CancellationToken ct = default)
    {
        lock (_rows)
        {
            var index = _rows.FindIndex(r => r.ParsedItemDraftId == parsedItemDraftId && r.UserId == userId);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _rows[index] = _rows[index] with { ReviewStatus = reviewStatus };
            return Task.FromResult(true);
        }
    }
}

/// <summary>A recognizer whose result each test scripts explicitly.</summary>
internal sealed class StubItemRecognizer : IItemRecognizer
{
    public Func<Guid, string, string, RecognitionResult> Behavior { get; set; } =
        (inboxEmailId, userId, _) => RecognitionResult.Parsed(
        [
            new NewParsedItemDraft(inboxEmailId, userId, null, null, "flight", "Flight ABC123", "SEA",
                new DateTime(2026, 8, 12, 9, 30, 0), "America/Los_Angeles", null, null, "ABC123", null, 0.9)
        ]);

    public string? LastAssembledText { get; private set; }

    public Task<RecognitionResult> RecognizeAsync(Guid inboxEmailId, string userId, string assembledText, CancellationToken ct = default)
    {
        LastAssembledText = assembledText;
        return Task.FromResult(Behavior(inboxEmailId, userId, assembledText));
    }
}

internal sealed class RecordingNotificationService : INotificationService
{
    private readonly List<NewNotification> _created = [];

    public IReadOnlyList<NewNotification> Created
    {
        get { lock (_created) { return _created.ToArray(); } }
    }

    public Task<NotificationRecord?> CreateAsync(NewNotification notification, CancellationToken ct)
    {
        lock (_created) { _created.Add(notification); }
        return Task.FromResult<NotificationRecord?>(null);
    }
}

/// <summary>A profile directory seeded with one traveler; tests can add ambiguous duplicates.</summary>
internal sealed class StubProfileDirectory : IUserProfileRepository
{
    private readonly List<(string UserId, string Email)> _entries = [];

    public StubProfileDirectory(string userId, string email) => _entries.Add((userId, email));

    public void Add(string userId, string email) => _entries.Add((userId, email));

    public Task<IReadOnlyList<string>> FindUserIdsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> matches = _entries
            .Where(e => string.Equals(e.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(e => e.UserId)
            .ToArray();
        return Task.FromResult(matches);
    }

    public Task<Contracts.Profile.UserProfileResponse?> GetAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<Contracts.Profile.UserProfileResponse?>(null);

    public Task<Contracts.Profile.UserProfileResponse> EnsureFromAuthenticatedUserAsync(string userId, string? firstName, string? lastName, string? displayName, string? email, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<Contracts.Profile.UserProfileResponse?> UpdateAsync(string userId, Contracts.Profile.UpdateUserProfileRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
