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
/// A forwarded transportation booking becomes a trip leg rather than a tracked item, and a
/// booking the email did not fully describe is refused with the missing detail named rather than
/// invented (FR-012 to FR-014, FR-019, FR-028 to FR-032, FR-038).
/// </summary>
public sealed class ConfirmDraftAsLegTests : IDisposable
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

    private Guid SeedTrip()
    {
        var id = Guid.NewGuid();
        _factory.Trips.Add(new TripDetail(id, "West Coast", null,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []));
        return id;
    }

    /// <summary>
    /// A flight draft carrying everything a leg needs. Individual tests blank one field to prove
    /// that field is the one reported back.
    /// </summary>
    private async Task<Guid> SeedTransportDraftAsync(
        Guid? tripId,
        DateTime? startLocal = null,
        string? startZone = "America/Los_Angeles",
        DateTime? endLocal = null,
        string? endZone = "America/New_York",
        string? origin = "SEA",
        string? destination = "JFK",
        string? mode = TransportationModes.Flight,
        string? title = "Flight ABC123",
        decimal? travelCost = 412.50m,
        string proposedOutcome = DraftOutcomes.Leg)
    {
        var userId = EmailIngestionApiFactory.TravelerUserId;
        var email = await _factory.Emails.InsertAsync(new NewInboxEmail(
            userId, $"seed-{Guid.NewGuid()}", "traveler@contoso.com", "trips@contoso.com", "Booking",
            "body", null, DateTimeOffset.UtcNow, Guid.NewGuid().ToString(), InboxEmailParseStatus.Parsed));

        var draft = await _factory.Drafts.InsertAsync(new NewParsedItemDraft(
            email!.InboxEmailId, userId, tripId, null, "flight", title, "SEA",
            startLocal ?? new DateTime(2026, 8, 12, 9, 30, 0), startZone,
            endLocal ?? new DateTime(2026, 8, 12, 17, 45, 0), endZone,
            "ABC123", null, 0.9,
            proposedOutcome, origin, destination, mode, travelCost, "USD"));

        return draft!.ParsedItemDraftId;
    }

    [Fact]
    public async Task AForwardedFlightBecomesATravelLegAndNotATrackedItem()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId);

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ConfirmParsedItemDraftResponse>(Web))!;
        Assert.Equal(DraftOutcome.Leg, body.Outcome);
        Assert.NotNull(body.CreatedTripLegId);
        Assert.Null(body.TrackedItemId);

        // The point of the whole feature: no tracked item is produced.
        Assert.Empty(_factory.TripItems.Rows);

        var created = Assert.Single(_factory.TripItems.CreatedLegs);
        Assert.Equal(tripId, created.TripId);
        Assert.Equal(TripLegKinds.Travel, created.Request.LegKind);
        Assert.Equal(TransportationModes.Flight, created.Request.TransportationMode);
        Assert.Equal("SEA", created.Request.Origin);
        Assert.Equal("JFK", created.Request.Destination);
        Assert.Equal(412.50m, created.Request.TravelCost);
        Assert.Equal("America/Los_Angeles", created.Request.StartTimeZoneId);
        Assert.Equal("America/New_York", created.Request.EndTimeZoneId);
    }

    /// <summary>
    /// The draft records which leg it became, the leg-outcome counterpart of the item trace
    /// feature 024 established (FR-038).
    /// </summary>
    [Fact]
    public async Task TheDraftRecordsTheLegItBecame()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId);

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);
        var body = (await response.Content.ReadFromJsonAsync<ConfirmParsedItemDraftResponse>(Web))!;

        var row = _factory.Drafts.Rows.Single();
        Assert.Equal("confirmed", row.ReviewStatus);
        Assert.Equal(body.CreatedTripLegId, row.CreatedTripLegId);
        Assert.Null(row.TrackedItemId);
        Assert.Equal(DraftOutcomes.Leg, row.ProposedOutcome);
    }

    /// <summary>
    /// Collaborators hear about an emailed leg exactly as they would a hand-entered one. The
    /// clarification session settled that this reuses the existing notification rather than
    /// adding a new kind.
    /// </summary>
    [Fact]
    public async Task ConfirmingALegRaisesTheSameNotificationTheLegFormDoes()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId);

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        var change = Assert.Single(_factory.ItineraryNotifications.Raised);
        Assert.Equal(ItineraryChangeKind.TripLegCreated, change.Change);
    }

    [Fact]
    public async Task ConfirmingALegRecordsTheLegAuditEntry()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId);

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Contains(_factory.Audit.Entries, e =>
            e.Operation == AuditOperations.TripLegCreate && e.Result == AuditResults.Success);
    }

    /// <summary>
    /// The end and the end zone are the two details a leg demands and an item does not, and the
    /// two recognition most often cannot supply. Each is named on its own field (FR-029).
    /// </summary>
    [Fact]
    public async Task AMissingEndIsNamedAgainstItsOwnFieldAndNothingIsWritten()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId, endLocal: null);

        // A null end cannot be expressed through the seed helper's default, so clear it directly.
        await _factory.Drafts.UpdateAsync(draftId, EmailIngestionApiFactory.TravelerUserId, new DraftUpdate(
            tripId, null, "flight", "Flight ABC123", "SEA",
            new DateTime(2026, 8, 12, 9, 30, 0), "America/Los_Angeles", null, "America/New_York",
            "ABC123", null, DraftOutcomes.Leg, "SEA", "JFK", TransportationModes.Flight, null));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("endLocal", error.Details!["field"]);
        Assert.Empty(_factory.TripItems.CreatedLegs);
        Assert.Empty(_factory.TripItems.Rows);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    [Theory]
    [InlineData(null, "endTimeZoneId")]
    public async Task AMissingEndZoneIsNamedAgainstItsOwnField(string? endZone, string expectedField)
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId, endZone: endZone);

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal(expectedField, error.Details!["field"]);
        Assert.Empty(_factory.TripItems.CreatedLegs);
    }

    [Fact]
    public async Task AMissingOriginIsNamedAgainstItsOwnField()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId, origin: null);

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("origin", error.Details!["field"]);
        Assert.Empty(_factory.TripItems.CreatedLegs);
    }

    [Fact]
    public async Task AMissingDestinationIsNamedAgainstItsOwnField()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId, destination: null);

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("destination", error.Details!["field"]);
        Assert.Empty(_factory.TripItems.CreatedLegs);
    }

    [Fact]
    public async Task ALegDraftWithoutATripIsRefusedAgainstTheTripField()
    {
        var draftId = await SeedTransportDraftAsync(null);

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await response.Content.ReadFromJsonAsync<ApiError>(Web))!;
        Assert.Equal("tripId", error.Details!["field"]);
        Assert.Empty(_factory.TripItems.CreatedLegs);
    }

    /// <summary>
    /// A leg outside the trip's dates is refused by the same validator the leg form runs, so an
    /// emailed leg can be no more permissive than a typed one (FR-019).
    /// </summary>
    [Fact]
    public async Task ALegOutsideTheTripDatesIsRefusedByTheSharedValidator()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId,
            startLocal: new DateTime(2027, 1, 4, 9, 30, 0),
            endLocal: new DateTime(2027, 1, 4, 17, 45, 0));

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_factory.TripItems.CreatedLegs);
        Assert.Equal("pending_review", _factory.Drafts.Rows.Single().ReviewStatus);
    }

    /// <summary>
    /// The regression guard for everything that is not transportation: a draft proposing an item
    /// still takes the original path and still produces a tracked item.
    /// </summary>
    [Fact]
    public async Task ADraftProposingAnItemStillBecomesATrackedItem()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId, proposedOutcome: DraftOutcomes.Item);

        var response = await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ConfirmParsedItemDraftResponse>(Web))!;
        Assert.Equal(DraftOutcome.Item, body.Outcome);
        Assert.NotNull(body.TrackedItemId);
        Assert.Null(body.CreatedTripLegId);
        Assert.Single(_factory.TripItems.Rows);
        Assert.Empty(_factory.TripItems.CreatedLegs);
    }

    /// <summary>
    /// Both outcomes leave a trace, and the two are mutually exclusive — a confirmed draft
    /// records an item id or a leg id, never both (FR-014, FR-038, FR-041).
    /// </summary>
    [Fact]
    public async Task ALegConfirmedDraftRecordsOnlyTheLegTrace()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId);

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        var row = _factory.Drafts.Rows.Single();
        Assert.Equal(DraftOutcomes.Leg, row.ProposedOutcome);
        Assert.NotNull(row.CreatedTripLegId);
        Assert.Null(row.TrackedItemId);
    }

    [Fact]
    public async Task AnItemConfirmedDraftRecordsOnlyTheItemTraceAsBefore()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId, proposedOutcome: DraftOutcomes.Item);

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        var row = _factory.Drafts.Rows.Single();
        Assert.Equal(DraftOutcomes.Item, row.ProposedOutcome);
        Assert.NotNull(row.TrackedItemId);
        Assert.Null(row.CreatedTripLegId);
    }

    /// <summary>
    /// Whichever it became, the draft still names the message it came from, so the path from a
    /// forwarded email to an itinerary entry stays walkable (FR-039, SC-007).
    /// </summary>
    [Fact]
    public async Task ALegConfirmedDraftStillNamesItsOriginatingMessage()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId);

        await CreateTravelerClient().PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        var row = _factory.Drafts.Rows.Single();
        Assert.Equal(_factory.Emails.Rows.Single().InboxEmailId, row.InboxEmailId);
    }

    /// <summary>A confirmed draft is out of the queue whichever entity it produced (FR-040).</summary>
    [Fact]
    public async Task AConfirmedLegDraftLeavesThePendingQueue()
    {
        var tripId = SeedTrip();
        var draftId = await SeedTransportDraftAsync(tripId);
        var client = CreateTravelerClient();

        await client.PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", null);

        var response = await client.GetAsync("/api/email-ingestion/drafts");
        var list = (await response.Content.ReadFromJsonAsync<ParsedItemDraftListResponse>(Web))!;
        Assert.DoesNotContain(list.Items, d => d.ParsedItemDraftId == draftId);
    }

    public void Dispose() => _factory.Dispose();
}
