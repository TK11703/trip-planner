using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// The review surface survived the move to relayed ingestion: drafts and inbox history stay
/// private to the traveler who owns them, and a draft cannot become a timeline item until it is
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

        var draft = await _factory.Drafts.InsertAsync(new NewParsedItemDraft(
            email!.InboxEmailId, userId, tripId, tripLegId, "flight", "Flight ABC123", "SEA",
            startLocal, "America/Los_Angeles", null, null, "ABC123", null, 0.9));

        return draft!.ParsedItemDraftId;
    }

    private static PlacementCandidateLeg CandidateLeg(int legStartDay, int legEndDay)
        => new(Guid.NewGuid(), "West Coast, August",
            new DateOnly(2026, 8, legStartDay), new DateOnly(2026, 8, legEndDay),
            Guid.NewGuid(), "San Francisco",
            new DateTimeOffset(2026, 8, legStartDay, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, legEndDay, 0, 0, 0, TimeSpan.Zero));

    /// <summary>Registers a trip the confirm path can resolve, optionally with one leg.</summary>
    private Guid SeedTrip(Guid? tripId = null, Guid? legId = null, int legStartDay = 12, int legEndDay = 16)
    {
        var id = tripId ?? Guid.NewGuid();
        TripLegDto[] legs = legId is { } lid
            ?
            [
                new TripLegDto(lid, id, "San Francisco", null, null,
                    new DateTime(2026, 8, legStartDay, 0, 0, 0), "UTC", "UTC",
                    new DateTime(2026, 8, legEndDay, 0, 0, 0), "UTC", "UTC", null, 0)
            ]
            : [];

        _factory.Trips.Add(new TripDetail(id, "West Coast", null,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, legs, []));
        _factory.TripItems.Legs.AddRange(legs);
        return id;
    }

    [Fact]
    public async Task DraftListOnlyReturnsTheCallersOwnDrafts()
    {
        await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        await SeedDraftAsync("someone-else");

        var drafts = await CreateTravelerClient().GetFromJsonAsync<ParsedItemDraftListResponse>("/api/email-ingestion/drafts", Web);

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
        var request = new UpdateParsedItemDraftRequest(
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
        Assert.Empty((await client.GetFromJsonAsync<ParsedItemDraftListResponse>("/api/email-ingestion/drafts", Web))!.Items);
    }

    [Fact]
    public async Task EditingOwnDraftPersistsTheTravelersCorrections()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        var leg = CandidateLeg(12, 16);
        _factory.Drafts.CandidateLegs.Add(leg);
        var request = new UpdateParsedItemDraftRequest(
            leg.TripId, leg.TripLegId, "flight", "Flight ABC123 (corrected)", "SEA Terminal A",
            new DateTime(2026, 8, 12, 10, 0, 0), "America/Los_Angeles", null, null, "ABC123", "Aisle seat");

        var response = await CreateTravelerClient().PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = _factory.Drafts.Rows.Single();
        Assert.Equal("Flight ABC123 (corrected)", stored.Title);
        Assert.Equal(leg.TripId, stored.TripId);
        Assert.Equal(leg.TripLegId, stored.TripLegId);
    }

    // Feature 024, FR-018: a leg from another trip would silently move the item somewhere the
    // traveler did not choose, so the edit is refused against the field that caused it.
    [Fact]
    public async Task ALegFromAnotherTripIsRefusedAgainstTheLegField()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        var mine = CandidateLeg(12, 16);
        var elsewhere = CandidateLeg(20, 24);
        _factory.Drafts.CandidateLegs.Add(mine);
        _factory.Drafts.CandidateLegs.Add(elsewhere);
        var request = new UpdateParsedItemDraftRequest(
            mine.TripId, elsewhere.TripLegId, "flight", "Flight ABC123", null,
            new DateTime(2026, 8, 13, 10, 0, 0), "America/Los_Angeles", null, null, null, null);

        var response = await CreateTravelerClient().PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("tripLegId", error.Details!["field"]);
        Assert.Null(_factory.Drafts.Rows.Single().TripLegId);
    }

    [Fact]
    public async Task ALegWithoutATripIsRefusedAgainstTheLegField()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        var leg = CandidateLeg(12, 16);
        _factory.Drafts.CandidateLegs.Add(leg);
        var request = new UpdateParsedItemDraftRequest(
            null, leg.TripLegId, "flight", "Flight ABC123", null,
            new DateTime(2026, 8, 13, 10, 0, 0), "America/Los_Angeles", null, null, null, null);

        var response = await CreateTravelerClient().PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("tripLegId", error.Details!["field"]);
    }

    // A correction can move the dates, so the answer carries a suggestion computed against the
    // dates the traveler just saved rather than the ones the email arrived with (FR-008).
    [Fact]
    public async Task ASuccessfulEditReturnsAPlacementRecomputedAgainstTheNewDates()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        var august = CandidateLeg(12, 16);
        _factory.Drafts.CandidateLegs.Add(august);
        var request = new UpdateParsedItemDraftRequest(
            null, null, "flight", "Flight ABC123", null,
            new DateTime(2026, 8, 13, 10, 0, 0), "UTC", null, null, null, null);

        var response = await CreateTravelerClient().PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ParsedItemDraftDto>(Web))!;
        Assert.Equal(DraftPlacementStatus.Matched, updated.Placement!.Status);
        Assert.Equal(august.TripLegId, updated.Placement.SuggestedTripLegId);
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

    // Feature 024, FR-011: a leg is optional. An item confirmed onto a trip with no leg lands in
    // the timeline's unassigned area, where the traveler can relate it to a leg later.
    [Fact]
    public async Task ADraftWithATripButNoLegCanStillBeConfirmed()
    {
        var tripId = SeedTrip();
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, tripId, startLocal: new DateTime(2026, 8, 12, 9, 30, 0));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ConfirmParsedItemDraftResponse>(Web))!;
        Assert.Equal(tripId, body.TripId);
        Assert.Null(body.TripLegId);
        Assert.Equal("confirmed", _factory.Drafts.Rows.Single().ReviewStatus);
        Assert.Null(_factory.TripItems.Rows.Single().TripLegId);
    }

    [Fact]
    public async Task ADraftWithoutAStartTimeCannotBeConfirmed()
    {
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, Guid.NewGuid(), Guid.NewGuid());

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    // Feature 024, FR-020 and SC-006: an email-created item obeys the leg window exactly as a
    // typed one does. The refusal names the end that fell outside, and nothing reaches the
    // timeline.
    [Fact]
    public async Task ConfirmingOntoALegThatDoesNotContainTheItemIsRefusedAndWritesNothing()
    {
        var legId = Guid.NewGuid();
        var tripId = SeedTrip(legId: legId, legStartDay: 12, legEndDay: 16);
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, tripId, legId,
            startLocal: new DateTime(2026, 8, 19, 9, 30, 0));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Contains(error.Details!["field"], new[] { "startLocal", "endLocal" });
        Assert.Empty(_factory.TripItems.Rows);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    // FR-023: a detail the parser could not read is named back rather than filled in with a
    // placeholder the traveler never wrote.
    [Fact]
    public async Task ConfirmingADraftMissingATitleNamesTheMissingDetail()
    {
        var tripId = SeedTrip();
        var email = await _factory.Emails.InsertAsync(new NewInboxEmail(
            EmailIngestionApiFactory.TravelerUserId, $"seed-{Guid.NewGuid()}", "traveler@contoso.com",
            "trips@contoso.com", "Booking", "body", null, DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString(), InboxEmailParseStatus.Parsed));
        var draft = await _factory.Drafts.InsertAsync(new NewParsedItemDraft(
            email!.InboxEmailId, EmailIngestionApiFactory.TravelerUserId, tripId, null, "flight",
            null, "SEA", new DateTime(2026, 8, 12, 9, 30, 0), "UTC", null, null, null, null, 0.4));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draft!.ParsedItemDraftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("title", error.Details!["field"]);
        Assert.Empty(_factory.TripItems.Rows);
    }

    [Fact]
    public async Task ConfirmingADraftMissingAStartTimezoneNamesTheMissingDetail()
    {
        var tripId = SeedTrip();
        var email = await _factory.Emails.InsertAsync(new NewInboxEmail(
            EmailIngestionApiFactory.TravelerUserId, $"seed-{Guid.NewGuid()}", "traveler@contoso.com",
            "trips@contoso.com", "Booking", "body", null, DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString(), InboxEmailParseStatus.Parsed));
        var draft = await _factory.Drafts.InsertAsync(new NewParsedItemDraft(
            email!.InboxEmailId, EmailIngestionApiFactory.TravelerUserId, tripId, null, "flight",
            "Flight ABC123", "SEA", new DateTime(2026, 8, 12, 9, 30, 0), null, null, null, null, null, 0.4));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draft!.ParsedItemDraftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("startTimeZoneId", error.Details!["field"]);
        Assert.Empty(_factory.TripItems.Rows);
    }

    // FR-024 and FR-025: a confirmed draft leaves the same trace a typed item does — an audit
    // entry, an itinerary notification, and a link back to the item it became.
    [Fact]
    public async Task ASuccessfulConfirmRecordsTheItemAndRaisesTheSameNotificationAManualAddRaises()
    {
        var legId = Guid.NewGuid();
        var tripId = SeedTrip(legId: legId);
        var draftId = await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, tripId, legId,
            startLocal: new DateTime(2026, 8, 13, 9, 30, 0));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ConfirmParsedItemDraftResponse>(Web))!;
        Assert.Equal(body.TrackedItemId, _factory.Drafts.Rows.Single().TrackedItemId);
        Assert.Contains(_factory.Audit.Entries, e => e.ResourceId == body.TrackedItemId.ToString() && e.Result == AuditResults.Success);
        var raised = Assert.Single(_factory.ItineraryNotifications.Raised);
        Assert.Equal(tripId, raised.TripId);
        Assert.Equal(ItineraryChangeKind.TripItemCreated, raised.Change);
    }

    // Feature 024: the review queue now tells the traveler where each draft could go. The
    // suggestion is computed per request and writes nothing (FR-006).
    [Fact]
    public async Task DraftListCarriesAPlacementForEveryDraft()
    {
        await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, startLocal: new DateTime(2026, 8, 13, 15, 0, 0));
        _factory.Drafts.CandidateLegs.Add(CandidateLeg(12, 16));

        var drafts = await CreateTravelerClient().GetFromJsonAsync<ParsedItemDraftListResponse>("/api/email-ingestion/drafts", Web);

        var only = Assert.Single(drafts!.Items);
        Assert.NotNull(only.Placement);
        Assert.Equal(DraftPlacementStatus.Matched, only.Placement!.Status);
        Assert.Equal(_factory.Drafts.CandidateLegs[0].TripId, only.Placement.SuggestedTripId);
        Assert.Equal(_factory.Drafts.CandidateLegs[0].TripLegId, only.Placement.SuggestedTripLegId);
    }

    [Fact]
    public async Task EvaluatingPlacementLeavesTheDraftUntouched()
    {
        await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId, startLocal: new DateTime(2026, 8, 13, 15, 0, 0));
        _factory.Drafts.CandidateLegs.Add(CandidateLeg(12, 16));
        var before = _factory.Drafts.Rows.Single();

        await CreateTravelerClient().GetFromJsonAsync<ParsedItemDraftListResponse>("/api/email-ingestion/drafts", Web);

        var after = Assert.Single(_factory.Drafts.Rows);
        Assert.Equal(before, after);
        Assert.Null(after.TripId);
        Assert.Null(after.TripLegId);
        Assert.Equal("pending_review", after.ReviewStatus);
    }

    [Fact]
    public async Task ADraftWithNoStartReportsInsufficientData()
    {
        await SeedDraftAsync(EmailIngestionApiFactory.TravelerUserId);
        _factory.Drafts.CandidateLegs.Add(CandidateLeg(12, 16));

        var drafts = await CreateTravelerClient().GetFromJsonAsync<ParsedItemDraftListResponse>("/api/email-ingestion/drafts", Web);

        var only = Assert.Single(drafts!.Items);
        Assert.Equal(DraftPlacementStatus.InsufficientData, only.Placement!.Status);
        Assert.Empty(only.Placement.Candidates);
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
