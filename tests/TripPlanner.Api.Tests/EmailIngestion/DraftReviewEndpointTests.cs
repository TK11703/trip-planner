using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// The review surface survived the move to relayed ingestion: drafts and inbox history stay
/// private to the traveler who owns them, and a draft cannot become a timeline event until it is
/// pointed at a trip and a leg (FR-013 to FR-016).
/// </summary>
public sealed class DraftReviewEndpointTests : IDisposable
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly EmailIngestionApiFactory _factory = new();

    private HttpClient CreateTravelerClient(string userId = EmailIngestionApiFactory.TravelerUserId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestScopeHeader, "access_as_user");
        return client;
    }

    private async Task<Guid> SeedDraftAsync(string userId, Guid? tripId = null, Guid? tripLegId = null, DateTime? startLocal = null)
    {
        var email = await _factory.Emails.InsertAsync(new NewInboxEmail(
            userId, $"seed-{Guid.NewGuid()}", "traveler@contoso.com", "trips@contoso.com", "Booking",
            "body", null, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), InboxEmailParseStatus.Parsed));

        var draft = await _factory.Drafts.InsertAsync(new NewParsedEventDraft(
            email!.InboxEmailId, userId, tripId, tripLegId, "flight", "Flight ABC123", "SEA",
            startLocal, "America/Los_Angeles", null, null, "ABC123", null, 0.9));

        return draft!.ParsedEventDraftId;
    }

    [Fact]
    public async Task DraftListOnlyReturnsTheCallersOwnDrafts()
    {
        await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        await SeedDraftAsync("someone-else");

        var drafts = await CreateTravelerClient().GetFromJsonAsync<ParsedEventDraftListResponse>("/api/email-ingestion/drafts", Web);

        var only = Assert.Single(drafts!.Items);
        Assert.Equal("Flight ABC123", only.Title);
    }

    [Fact]
    public async Task InboxHistoryOnlyReturnsTheCallersOwnMessages()
    {
        await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        await SeedDraftAsync("someone-else");

        var inbox = await CreateTravelerClient().GetFromJsonAsync<InboxEmailListResponse>("/api/email-ingestion/inbox", Web);

        Assert.Single(inbox!.Items);
    }

    [Fact]
    public async Task AnotherTravelersDraftCannotBeEdited()
    {
        var draftId = await SeedDraftAsync("someone-else");
        var request = new UpdateParsedEventDraftRequest(
            Guid.NewGuid(), Guid.NewGuid(), "flight", "Hijacked", null, null, null, null, null, null, null);

        var response = await CreateTravelerClient().PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Flight ABC123", _factory.Drafts.Rows.Single().Title);
    }

    [Fact]
    public async Task AnotherTravelersDraftCannotBeDiscarded()
    {
        var draftId = await SeedDraftAsync("someone-else");

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/discard", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    [Fact]
    public async Task DiscardingOwnDraftRemovesItFromTheReviewQueue()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        var client = CreateTravelerClient();

        var response = await client.PostAsync($"/api/email-ingestion/drafts/{draftId}/discard", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("discarded", _factory.Drafts.Rows.Single().ReviewStatus);
        Assert.Empty((await client.GetFromJsonAsync<ParsedEventDraftListResponse>("/api/email-ingestion/drafts", Web))!.Items);
    }

    [Fact]
    public async Task EditingOwnDraftPersistsTheTravelersCorrections()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var request = new UpdateParsedEventDraftRequest(
            tripId, legId, "flight", "Flight ABC123 (corrected)", "SEA Terminal A",
            new DateTime(2026, 8, 12, 10, 0, 0), "America/Los_Angeles", null, null, "ABC123", "Aisle seat");

        var response = await CreateTravelerClient().PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = _factory.Drafts.Rows.Single();
        Assert.Equal("Flight ABC123 (corrected)", stored.Title);
        Assert.Equal(tripId, stored.TripId);
        Assert.Equal(legId, stored.TripLegId);
    }

    [Fact]
    public async Task AnotherTravelersDraftCannotBeConfirmed()
    {
        var draftId = await SeedDraftAsync("someone-else", Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 12, 9, 30, 0));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    [Fact]
    public async Task ADraftWithoutATripAndLegCannotBeConfirmed()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, startLocal: new DateTime(2026, 8, 12, 9, 30, 0));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    [Fact]
    public async Task ADraftWithoutAStartTimeCannotBeConfirmed()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, Guid.NewGuid(), Guid.NewGuid());

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    [Fact]
    public async Task ReprocessingAnotherTravelersMessageIsNotFound()
    {
        await SeedDraftAsync("someone-else");
        var inboxEmailId = _factory.Emails.Rows.Single().InboxEmailId;

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/inbox/{inboxEmailId}/reprocess", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReprocessingOwnMessageReturnsAFreshOutcomeImmediately()
    {
        await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        var inboxEmailId = _factory.Emails.Rows.Single().InboxEmailId;

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/inbox/{inboxEmailId}/reprocess", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<IngestRelayMessageResponse>(Web))!;
        Assert.Equal(EmailIngestionOutcome.Parsed, body.Status);
        Assert.Equal("parsed", _factory.Emails.Rows.Single().ParseStatus);
    }

    public void Dispose() => _factory.Dispose();
}
