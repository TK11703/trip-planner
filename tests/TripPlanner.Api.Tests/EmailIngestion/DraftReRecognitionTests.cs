using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// Drafts recognized before transport recognition catch up the first time the traveler opens
/// them — without losing an edit, without a second provider call, and above all without adding a
/// second draft to the queue (FR-045, FR-046, FR-047).
/// </summary>
public sealed class DraftReRecognitionTests : IDisposable
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

    /// <summary>A draft as it looked before this feature: no route, no mode, state `pending`.</summary>
    private async Task<Guid> SeedLegacyDraftAsync(
        string userId = EmailIngestionApiFactory.TravelerUserId,
        string? confirmationCode = "ABC123",
        string? title = "Flight ABC123")
    {
        var email = await _factory.Emails.InsertAsync(new NewInboxEmail(
            userId, $"seed-{Guid.NewGuid()}", "traveler@contoso.com", "trips@contoso.com",
            "Your flight", "Departing SEA for JFK", null, DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString(), InboxEmailParseStatus.Parsed));

        var draft = await _factory.Drafts.InsertAsync(new NewParsedItemDraft(
            email!.InboxEmailId, userId, null, null, "flight", title, null,
            new DateTime(2026, 8, 12, 9, 30, 0), "America/Los_Angeles", null, null,
            confirmationCode, null, 0.9));

        _factory.Drafts.Replace(draft!.ParsedItemDraftId, d => d with
        {
            TransportRecognitionState = DraftRecognitionStates.Pending
        });

        return draft.ParsedItemDraftId;
    }

    private void RecognizeAs(params NewParsedItemDraft[] results)
        => _factory.Recognizer.Behavior = (_, _, _) => RecognitionResult.Parsed(results);

    private static NewParsedItemDraft Recognized(
        Guid inboxEmailId, string userId,
        string? confirmationCode = "ABC123",
        string origin = "SEA",
        string destination = "JFK",
        DateTime? start = null) =>
        new(inboxEmailId, userId, null, null, "flight", "Recognized flight", "SEA",
            start ?? new DateTime(2026, 8, 12, 9, 30, 0), "America/Los_Angeles",
            new DateTime(2026, 8, 12, 17, 45, 0), "America/New_York",
            confirmationCode, null, 0.95,
            DraftOutcomes.Leg, origin, destination, TransportationModes.Flight, 412.50m, "USD");

    [Fact]
    public async Task ALegacyDraftGainsItsRouteAndBecomesALegProposal()
    {
        var draftId = await SeedLegacyDraftAsync();
        var email = _factory.Emails.Rows.Single();
        RecognizeAs(Recognized(email.InboxEmailId, EmailIngestionApiFactory.TravelerUserId));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<ParsedItemDraftDto>(Web))!;
        Assert.Equal(DraftOutcome.Leg, dto.ProposedOutcome);
        Assert.Equal("SEA", dto.Origin);
        Assert.Equal("JFK", dto.Destination);
        Assert.Equal(TransportationModes.Flight, dto.TransportationMode);
        Assert.Equal(DraftRecognitionState.Current, dto.TransportRecognitionState);
    }

    /// <summary>
    /// The top risk on this feature. `RelayMessageProcessor.ReprocessAsync` ends in an insert, so
    /// reusing it here would add a draft to the queue every time a legacy one was opened.
    /// </summary>
    [Fact]
    public async Task ReRecognitionNeverAddsADraftToTheQueue()
    {
        var draftId = await SeedLegacyDraftAsync();
        var email = _factory.Emails.Rows.Single();
        RecognizeAs(Recognized(email.InboxEmailId, EmailIngestionApiFactory.TravelerUserId));

        var before = _factory.Drafts.Rows.Count;
        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);
        var after = _factory.Drafts.Rows.Count;

        Assert.Equal(before, after);
        Assert.Equal(1, after);
    }

    /// <summary>A value the traveler supplied outranks anything the recognizer reads.</summary>
    [Fact]
    public async Task ATravelerEditSurvivesReRecognition()
    {
        var draftId = await SeedLegacyDraftAsync(title: "The traveler's own title");
        var email = _factory.Emails.Rows.Single();
        RecognizeAs(Recognized(email.InboxEmailId, EmailIngestionApiFactory.TravelerUserId));

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Equal("The traveler's own title", _factory.Drafts.Rows.Single().Title);
    }

    /// <summary>
    /// A field the traveler deliberately cleared stays cleared. A null check alone would read
    /// that decision as an empty slot and refill it.
    /// </summary>
    [Fact]
    public async Task ADeliberatelyClearedFieldStaysCleared()
    {
        var draftId = await SeedLegacyDraftAsync(confirmationCode: null);
        _factory.Drafts.Replace(draftId, d => d with { TravelerEditedFields = ["confirmation_code"] });
        var email = _factory.Emails.Rows.Single();
        RecognizeAs(Recognized(email.InboxEmailId, EmailIngestionApiFactory.TravelerUserId));

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Null(_factory.Drafts.Rows.Single().ConfirmationCode);
    }

    /// <summary>FR-034 reserves the end and its zone for the traveler, recognized or not.</summary>
    [Fact]
    public async Task TheEndAndItsZoneAreNeverMerged()
    {
        var draftId = await SeedLegacyDraftAsync();
        var email = _factory.Emails.Rows.Single();
        RecognizeAs(Recognized(email.InboxEmailId, EmailIngestionApiFactory.TravelerUserId));

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        var stored = _factory.Drafts.Rows.Single();
        Assert.Null(stored.EndLocal);
        Assert.Null(stored.EndTimeZoneId);
    }

    [Fact]
    public async Task AProviderOutageLeavesTheDraftUsableOnTheItemPath()
    {
        var draftId = await SeedLegacyDraftAsync();
        _factory.Recognizer.Behavior = (_, _, _) => RecognitionResult.Failed();

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<ParsedItemDraftDto>(Web))!;
        Assert.Equal(DraftRecognitionState.Unavailable, dto.TransportRecognitionState);
        Assert.Equal(DraftOutcome.Item, dto.ProposedOutcome);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    [Fact]
    public async Task RecognizingNothingAlsoSettlesTheState()
    {
        var draftId = await SeedLegacyDraftAsync();
        _factory.Recognizer.Behavior = (_, _, _) => RecognitionResult.Unsupported();

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(DraftRecognitionStates.Unavailable, _factory.Drafts.Rows.Single().TransportRecognitionState);
    }

    /// <summary>A draft is re-examined once. Reopening it must not cost a second provider call.</summary>
    [Fact]
    public async Task ASecondCallDoesNotReachTheProvider()
    {
        var draftId = await SeedLegacyDraftAsync();
        var email = _factory.Emails.Rows.Single();
        RecognizeAs(Recognized(email.InboxEmailId, EmailIngestionApiFactory.TravelerUserId));
        var client = CreateTravelerClient();

        await client.PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);
        var afterFirst = _factory.Recognizer.Calls;

        await client.PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Equal(afterFirst, _factory.Recognizer.Calls);
    }

    /// <summary>
    /// One message can describe several bookings, so the re-read is attributed back by
    /// confirmation code before anything else.
    /// </summary>
    [Fact]
    public async Task TheMatchingBookingIsSelectedByConfirmationCode()
    {
        var draftId = await SeedLegacyDraftAsync(confirmationCode: "SECOND");
        var email = _factory.Emails.Rows.Single();
        var userId = EmailIngestionApiFactory.TravelerUserId;
        RecognizeAs(
            Recognized(email.InboxEmailId, userId, confirmationCode: "FIRST", origin: "AAA", destination: "BBB"),
            Recognized(email.InboxEmailId, userId, confirmationCode: "SECOND", origin: "CCC", destination: "DDD"));

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        var stored = _factory.Drafts.Rows.Single();
        Assert.Equal("CCC", stored.Origin);
        Assert.Equal("DDD", stored.Destination);
    }

    [Fact]
    public async Task AnotherTravelersDraftCannotBeReRecognized()
    {
        var draftId = await SeedLegacyDraftAsync("someone-else");

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownDraftIsNotFound()
    {
        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{Guid.NewGuid()}/re-recognize", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Feature 024's SC-008: the queue is a read. Putting a provider call in it would charge
    /// every traveler for every draft on every visit.
    /// </summary>
    [Fact]
    public async Task ListingDraftsNeverReachesTheProvider()
    {
        await SeedLegacyDraftAsync();
        await SeedLegacyDraftAsync();
        var before = _factory.Recognizer.Calls;

        var response = await CreateTravelerClient().GetAsync("/api/email-ingestion/drafts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, _factory.Recognizer.Calls);
    }

    /// <summary>
    /// The outage that is easiest to get wrong: a recognizer that cannot be constructed at all —
    /// an unconfigured endpoint, a credential that will not load. Injecting it directly would
    /// move that failure into dependency resolution, where this method never runs, and the draft
    /// would stay `pending` forever: every open would retry and every retry would fail (FR-047).
    /// </summary>
    [Fact]
    public async Task ARecognizerThatCannotBeConstructedStillSettlesTheState()
    {
        var draftId = await SeedLegacyDraftAsync();
        _factory.RecognizerFactory = () => throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is required for email parsing.");

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/re-recognize", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<ParsedItemDraftDto>(Web))!;
        Assert.Equal(DraftRecognitionState.Unavailable, dto.TransportRecognitionState);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    public void Dispose() => _factory.Dispose();
}
