using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Api.Security;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Notifications;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Trips;
using TripPlanner.Database.TripSharing;
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
    public RecordingTripItemRepository TripItems { get; } = new();

    /// <summary>Trips the confirm path can resolve, keyed by trip id. Tests seed what they need.</summary>
    public StubTripReadRepository Trips { get; } = new();
    public RecordingAuditRepository Audit { get; } = new();
    public RecordingItineraryNotificationService ItineraryNotifications { get; } = new();

    /// <summary>
    /// How the recognizer is obtained. Defaults to the stub; a test can replace it with a
    /// factory that throws, modelling a provider that cannot even be constructed.
    /// </summary>
    public Func<IItemRecognizer> RecognizerFactory { get; set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        RecognizerFactory ??= () => Recognizer;
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInboxEmailRepository>();
            services.RemoveAll<IEmailAttachmentRepository>();
            services.RemoveAll<IParsedItemDraftRepository>();
            services.RemoveAll<IItemRecognizer>();
            services.RemoveAll<INotificationService>();
            services.RemoveAll<IUserProfileRepository>();
            services.RemoveAll<ITripItemRepository>();
            services.RemoveAll<ITripReadRepository>();
            services.RemoveAll<ITripAccessResolver>();
            services.RemoveAll<IAuditRepository>();
            services.RemoveAll<IItineraryNotificationService>();
            services.RemoveAll<Func<IItemRecognizer>>();

            services.AddSingleton<IInboxEmailRepository>(Emails);
            services.AddSingleton<IEmailAttachmentRepository>(Attachments);
            services.AddSingleton<IParsedItemDraftRepository>(Drafts);
            services.AddSingleton<IItemRecognizer>(Recognizer);
            services.AddSingleton<INotificationService>(Notifications);
            services.AddSingleton<IUserProfileRepository>(Profiles);
            services.AddSingleton<ITripItemRepository>(TripItems);
            services.AddSingleton<ITripReadRepository>(Trips);
            services.AddSingleton<ITripAccessResolver>(new StubTripAccessResolver(Trips));
            services.AddSingleton<IAuditRepository>(Audit);
            services.AddSingleton<IItineraryNotificationService>(ItineraryNotifications);

            // Lets a test model a recognizer that cannot even be constructed.
            services.AddScoped<Func<IItemRecognizer>>(_ => () => RecognizerFactory());
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

    public Task<IReadOnlyList<InboxEmailSummary>> GetListAsync(string userId, int limit, CancellationToken ct = default)
    {
        lock (_rows)
        {
            IReadOnlyList<InboxEmailSummary> result = _rows
                .Where(r => r.UserId == userId)
                .Take(limit)
                .Select(r => new InboxEmailSummary(r.InboxEmailId, r.UserId, r.Sender, r.Subject, r.ReceivedAt, r.ParseStatus, r.CreatedAtUtc))
                .ToArray();
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
            draft.ConfirmationCode, draft.Notes, draft.Confidence, "pending_review", DateTimeOffset.UtcNow,
            TrackedItemId: null,
            ProposedOutcome: draft.ProposedOutcome,
            Origin: draft.Origin,
            Destination: draft.Destination,
            TransportationMode: draft.TransportationMode,
            TravelCost: draft.TravelCost,
            TravelCostCurrency: draft.TravelCostCurrency,
            CreatedTripLegId: null,
            TransportRecognitionState: DraftRecognitionStates.Current,
            TravelerEditedFields: []);
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

            var current = _rows[index];

            // Mirrors the FR-046 bookkeeping the real UPDATE does in SQL: a field the traveler
            // changes by hand is recorded, including when they clear it.
            var edited = new HashSet<string>(current.TravelerEditedFields ?? [], StringComparer.Ordinal);
            if (update.ProposedOutcome != current.ProposedOutcome) edited.Add("proposed_outcome");
            if (update.Origin != current.Origin) edited.Add("origin");
            if (update.Destination != current.Destination) edited.Add("destination");
            if (update.TransportationMode != current.TransportationMode) edited.Add("transportation_mode");
            if (update.TravelCost != current.TravelCost) edited.Add("travel_cost");

            _rows[index] = current with
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
                Notes = update.Notes,
                ProposedOutcome = update.ProposedOutcome,
                Origin = update.Origin,
                Destination = update.Destination,
                TransportationMode = update.TransportationMode,
                TravelCost = update.TravelCost,
                TravelerEditedFields = edited.ToArray()
            };
            return Task.FromResult<ParsedItemDraftRecord?>(_rows[index]);
        }
    }

    public Task<bool> SetReviewStatusAsync(
        Guid parsedItemDraftId,
        string userId,
        string reviewStatus,
        Guid? trackedItemId = null,
        string? proposedOutcome = null,
        Guid? createdTripLegId = null,
        CancellationToken ct = default)
    {
        lock (_rows)
        {
            var index = _rows.FindIndex(r => r.ParsedItemDraftId == parsedItemDraftId && r.UserId == userId);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _rows[index] = _rows[index] with
            {
                ReviewStatus = reviewStatus,
                TrackedItemId = trackedItemId ?? _rows[index].TrackedItemId,
                CreatedTripLegId = createdTripLegId ?? _rows[index].CreatedTripLegId,
                ProposedOutcome = proposedOutcome ?? _rows[index].ProposedOutcome
            };
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Mirrors `MergeParsedItemDraftRecognition.sql`: a field is written only when it is both
    /// null and unedited. This is a restatement, not the rule itself — the rule is SQL, and
    /// `ParsedItemDraftMergeTests` is what actually verifies it against PostgreSQL.
    /// </summary>
    public Task<ParsedItemDraftRecord?> MergeRecognitionAsync(
        Guid parsedItemDraftId,
        string userId,
        DraftRecognitionMerge merge,
        CancellationToken ct = default)
    {
        lock (_rows)
        {
            var index = _rows.FindIndex(r =>
                r.ParsedItemDraftId == parsedItemDraftId
                && r.UserId == userId
                && r.ReviewStatus == "pending_review");
            if (index < 0)
            {
                return Task.FromResult<ParsedItemDraftRecord?>(null);
            }

            var current = _rows[index];
            var edited = new HashSet<string>(current.TravelerEditedFields ?? [], StringComparer.Ordinal);

            string? Merge(string? existing, string? incoming, string field)
                => existing is null && !edited.Contains(field) ? incoming : existing;

            _rows[index] = current with
            {
                // Recognition may promote an item to a leg, never the reverse, and never over an
                // outcome the traveler chose.
                ProposedOutcome = merge.ProposedOutcome is not null
                                  && current.ProposedOutcome == DraftOutcomes.Item
                                  && !edited.Contains("proposed_outcome")
                    ? merge.ProposedOutcome
                    : current.ProposedOutcome,
                Origin = Merge(current.Origin, merge.Origin, "origin"),
                Destination = Merge(current.Destination, merge.Destination, "destination"),
                TransportationMode = Merge(current.TransportationMode, merge.TransportationMode, "transportation_mode"),
                TravelCost = current.TravelCost is null && !edited.Contains("travel_cost")
                    ? merge.TravelCost
                    : current.TravelCost,
                TravelCostCurrency = Merge(current.TravelCostCurrency, merge.TravelCostCurrency, "travel_cost_currency"),
                Title = Merge(current.Title, merge.Title, "title"),
                Location = Merge(current.Location, merge.Location, "location"),
                ConfirmationCode = Merge(current.ConfirmationCode, merge.ConfirmationCode, "confirmation_code"),
                StartLocal = current.StartLocal is null && !edited.Contains("start_local")
                    ? merge.StartLocal
                    : current.StartLocal,
                StartTimeZoneId = Merge(current.StartTimeZoneId, merge.StartTimeZoneId, "start_timezone_id"),
                // The end and its zone are never merged (FR-034).
                TransportRecognitionState = merge.TransportRecognitionState
            };

            return Task.FromResult<ParsedItemDraftRecord?>(_rows[index]);
        }
    }

    /// <summary>
    /// Rewrites a stored draft, so a test can stage the state a real legacy row would have —
    /// `pending` recognition, or a field the traveler has already edited.
    /// </summary>
    public void Replace(Guid parsedItemDraftId, Func<ParsedItemDraftRecord, ParsedItemDraftRecord> update)
    {
        lock (_rows)
        {
            var index = _rows.FindIndex(r => r.ParsedItemDraftId == parsedItemDraftId);
            if (index >= 0) _rows[index] = update(_rows[index]);
        }
    }

    /// <summary>Legs the caller could place a draft on. Tests script this directly.</summary>
    public List<PlacementCandidateLeg> CandidateLegs { get; } = [];

    public Task<IReadOnlyList<PlacementCandidateLeg>> GetPlacementCandidateLegsAsync(string userId, string? callerEmail, CancellationToken ct = default)
    {
        lock (_rows)
        {
            IReadOnlyList<PlacementCandidateLeg> result = CandidateLegs.ToArray();
            return Task.FromResult(result);
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

    /// <summary>How many times recognition ran, so a test can prove a read path never calls it.</summary>
    public int Calls { get; private set; }

    public Task<RecognitionResult> RecognizeAsync(Guid inboxEmailId, string userId, string assembledText, CancellationToken ct = default)
    {
        Calls++;
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

/// <summary>
/// Records the items a confirmed draft creates so tests can assert what reached the timeline —
/// in particular whether a leg was attached (Feature 024, FR-011).
/// </summary>
internal sealed class RecordingTripItemRepository : ITripItemRepository
{
    private readonly List<(Guid TripId, Guid? TripLegId, CreateTrackedItemRequest Request)> _rows = [];

    public IReadOnlyList<(Guid TripId, Guid? TripLegId, CreateTrackedItemRequest Request)> Rows
    {
        get { lock (_rows) { return _rows.ToArray(); } }
    }

    public Task<Guid?> CreateTrackedItemAsync(string ownerUserId, Guid tripId, CreateTrackedItemRequest request, DateTimeOffset nowUtc, CancellationToken ct)
    {
        lock (_rows) { _rows.Add((tripId, request.TripLegId, request)); }
        return Task.FromResult<Guid?>(Guid.NewGuid());
    }

    public Task<IReadOnlyList<TripLegDto>> GetLegsAsync(string ownerUserId, Guid tripId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<TripLegDto>>(Legs.Where(l => l.TripId == tripId).ToArray());

    /// <summary>Legs the confirm path validates against. Tests seed what the scenario needs.</summary>
    public List<TripLegDto> Legs { get; } = [];

    private readonly List<(Guid TripId, CreateTripLegRequest Request)> _createdLegs = [];

    /// <summary>Legs this repository was asked to create. Empty proves nothing was written.</summary>
    public IReadOnlyList<(Guid TripId, CreateTripLegRequest Request)> CreatedLegs
    {
        get { lock (_createdLegs) { return _createdLegs.ToArray(); } }
    }

    public Task<IReadOnlyList<TrackedItemDto>> GetTrackedItemsAsync(string ownerUserId, Guid tripId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TrackedItemDto>>([]);
    public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(string ownerUserId, Guid tripId, CancellationToken ct) => Task.FromResult<TripLegDefaultsResponse?>(new TripLegDefaultsResponse("UTC", "UTC", "profile"));

    public Task<Guid?> CreateLegAsync(string ownerUserId, Guid tripId, CreateTripLegRequest request, DateTimeOffset nowUtc, CancellationToken ct)
    {
        lock (_createdLegs) { _createdLegs.Add((tripId, request)); }
        return Task.FromResult<Guid?>(Guid.NewGuid());
    }

    public Task<int> UpdateLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct) => Task.FromResult(1);
    public Task<int> DeleteLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct) => Task.FromResult(1);
    public Task<int> UpdateTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct) => Task.FromResult(1);
    public Task<int> DeleteTrackedItemAsync(string ownerUserId, Guid tripId, Guid trackedItemId, CancellationToken ct) => Task.FromResult(1);
    public Task<int> CountItemsForLegAsync(string ownerUserId, Guid tripId, Guid tripLegId, CancellationToken ct) => Task.FromResult(0);
}

/// <summary>Trips the confirm path can read. A trip absent here is treated as not found.</summary>
internal sealed class StubTripReadRepository : ITripReadRepository
{
    private readonly Dictionary<Guid, TripDetail> _trips = [];

    public void Add(TripDetail trip)
    {
        lock (_trips) { _trips[trip.TripId] = trip; }
    }

    public bool Contains(Guid tripId)
    {
        lock (_trips) { return _trips.ContainsKey(tripId); }
    }

    public Task<TripDetail?> GetDetailAsync(string ownerUserId, Guid tripId, CancellationToken cancellationToken)
    {
        lock (_trips) { return Task.FromResult(_trips.TryGetValue(tripId, out var trip) ? trip : null); }
    }

    public Task<TripListResponse> GetPageAsync(string ownerUserId, string? callerEmail, int page, int pageSize, CancellationToken cancellationToken)
        => Task.FromResult(new TripListResponse([], page, pageSize, 0));

    public Task<IReadOnlyList<TripSummary>> GetRecentAsync(string ownerUserId, int limit, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TripSummary>>([]);
}

/// <summary>Grants owner access to any seeded trip, and none to anything else.</summary>
internal sealed class StubTripAccessResolver : ITripAccessResolver
{
    private readonly StubTripReadRepository _trips;

    public StubTripAccessResolver(StubTripReadRepository trips) => _trips = trips;

    public Task<TripAccess?> ResolveAsync(string callerUserId, Guid tripId, CancellationToken ct)
        => Task.FromResult(_trips.Contains(tripId)
            ? new TripAccess(callerUserId, TripAccessLevel.Owner)
            : null);
}

internal sealed class RecordingAuditRepository : IAuditRepository
{
    private readonly List<(string Operation, string? ResourceId, string Result)> _entries = [];

    public IReadOnlyList<(string Operation, string? ResourceId, string Result)> Entries
    {
        get { lock (_entries) { return _entries.ToArray(); } }
    }

    public Task RecordAsync(string? userId, string operation, string resourceType, string? resourceId, string result, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
    {
        lock (_entries) { _entries.Add((operation, resourceId, result)); }
        return Task.CompletedTask;
    }
}

internal sealed class RecordingItineraryNotificationService : IItineraryNotificationService
{
    private readonly List<(Guid TripId, ItineraryChangeKind Change)> _raised = [];

    public IReadOnlyList<(Guid TripId, ItineraryChangeKind Change)> Raised
    {
        get { lock (_raised) { return _raised.ToArray(); } }
    }

    public Task NotifyChangeAsync(Guid tripId, string ownerUserId, string actorUserId, string? actorDisplayName, ItineraryChangeKind change, Guid entityId, CancellationToken ct)
    {
        lock (_raised) { _raised.Add((tripId, change)); }
        return Task.CompletedTask;
    }
}
