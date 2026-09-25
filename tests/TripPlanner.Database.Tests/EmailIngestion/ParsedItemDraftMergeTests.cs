using Dapper;
using Npgsql;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Database.Tests.EmailIngestion;

/// <summary>
/// The FR-046 merge rule, exercised against a real PostgreSQL instance.
///
/// This runs on a container rather than a fake on purpose. The rule is expressed entirely in SQL
/// — a field is written only when it is <em>both</em> null <em>and</em> absent from
/// <c>traveler_edited_fields</c> — and an in-memory stand-in would simply restate the C# author's
/// belief about what that SQL does. Feature 028 shipped two defects that only a live database
/// surfaced: a <c>text[]</c> column that Dapper could not materialize through a positional
/// record, and a caller that quietly reset a column it did not name.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class ParsedItemDraftMergeTests
{
    private readonly PostgresFixture _fixture;
    private readonly ParsedItemDraftRepository _repository;

    public ParsedItemDraftMergeTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _repository = new ParsedItemDraftRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// A draft as it looked before this feature: recognized, pending, and carrying none of the
    /// transport detail. Optional arguments let a single test pre-set one field or mark one as
    /// traveler-edited.
    /// </summary>
    private async Task<(Guid DraftId, string UserId)> SeedLegacyDraftAsync(
        string? origin = null,
        string? title = null,
        string? confirmationCode = null,
        string[]? travelerEdited = null,
        string recognitionState = DraftRecognitionStates.Pending,
        string proposedOutcome = DraftOutcomes.Item)
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var emailId = Guid.NewGuid();
        var draftId = Guid.NewGuid();

        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO inbox_emails (inbox_email_id, user_id, sender, subject, body_text, received_at, parse_status, dedupe_hash)
            VALUES (@emailId, @userId, 'a@b.c', 'Booking', 'body', now(), 'parsed', @hash);

            INSERT INTO parsed_item_drafts (
                parsed_item_draft_id, inbox_email_id, user_id, item_type, title,
                confirmation_code, origin, confidence, review_status,
                proposed_outcome, transport_recognition_state, traveler_edited_fields)
            VALUES (
                @draftId, @emailId, @userId, 'flight', @title,
                @confirmationCode, @origin, 0.9, 'pending_review',
                @proposedOutcome, @recognitionState, @travelerEdited);
            """,
            new
            {
                emailId,
                draftId,
                userId,
                hash = Guid.NewGuid().ToString(),
                title,
                confirmationCode,
                origin,
                proposedOutcome,
                recognitionState,
                travelerEdited = travelerEdited ?? []
            });

        return (draftId, userId);
    }

    private static DraftRecognitionMerge FullMerge(string state = DraftRecognitionStates.Current) => new(
        ProposedOutcome: DraftOutcomes.Leg,
        Origin: "SEA",
        Destination: "JFK",
        TransportationMode: "flight",
        TravelCost: 412.50m,
        TravelCostCurrency: "USD",
        Title: "Recognized title",
        Location: "Seattle",
        ConfirmationCode: "REC123",
        StartLocal: new DateTime(2026, 8, 12, 9, 30, 0),
        StartTimeZoneId: "America/Los_Angeles",
        TransportRecognitionState: state);

    [Fact]
    public async Task RecognitionFillsEveryDetailTheDraftWasMissing()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync();

        var merged = await _repository.MergeRecognitionAsync(draftId, userId, FullMerge());

        Assert.NotNull(merged);
        Assert.Equal(DraftOutcomes.Leg, merged!.ProposedOutcome);
        Assert.Equal("SEA", merged.Origin);
        Assert.Equal("JFK", merged.Destination);
        Assert.Equal("flight", merged.TransportationMode);
        Assert.Equal(412.50m, merged.TravelCost);
        Assert.Equal("USD", merged.TravelCostCurrency);
        Assert.Equal(DraftRecognitionStates.Current, merged.TransportRecognitionState);
    }

    /// <summary>A value the draft already carries is never overwritten, edited or not.</summary>
    [Fact]
    public async Task AnExistingValueSurvivesRecognition()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync(origin: "Traveler's own origin");

        var merged = await _repository.MergeRecognitionAsync(draftId, userId, FullMerge());

        Assert.Equal("Traveler's own origin", merged!.Origin);
        // The fields that were genuinely empty still get filled.
        Assert.Equal("JFK", merged.Destination);
    }

    /// <summary>
    /// The case the whole rule exists for. A traveler who clears a field has made a decision;
    /// a null check alone would read that decision as an empty slot and refill it.
    /// </summary>
    [Fact]
    public async Task ADeliberatelyClearedFieldStaysCleared()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync(
            confirmationCode: null,
            travelerEdited: ["confirmation_code"]);

        var merged = await _repository.MergeRecognitionAsync(draftId, userId, FullMerge());

        Assert.Null(merged!.ConfirmationCode);
        Assert.Equal("Recognized title", merged.Title);
    }

    [Fact]
    public async Task ATravelerEditedOutcomeIsNotReclassified()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync(travelerEdited: ["proposed_outcome"]);

        var merged = await _repository.MergeRecognitionAsync(draftId, userId, FullMerge());

        Assert.Equal(DraftOutcomes.Item, merged!.ProposedOutcome);
    }

    /// <summary>
    /// A draft the traveler already moved to the leg path is left alone: recognition may promote
    /// an item to a leg, never the reverse.
    /// </summary>
    [Fact]
    public async Task ADraftAlreadyOnTheLegPathIsNotDemoted()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync(proposedOutcome: DraftOutcomes.Leg);

        var merged = await _repository.MergeRecognitionAsync(
            draftId, userId, FullMerge() with { ProposedOutcome = DraftOutcomes.Item });

        Assert.Equal(DraftOutcomes.Leg, merged!.ProposedOutcome);
    }

    /// <summary>
    /// FR-034 reserves the end and its zone for the traveler, so the merge must not carry them
    /// even though recognition can produce them. There is no parameter for either — this asserts
    /// the columns are untouched rather than that the value was ignored.
    /// </summary>
    [Fact]
    public async Task TheEndAndItsZoneAreNeverMerged()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync();

        var merged = await _repository.MergeRecognitionAsync(draftId, userId, FullMerge());

        Assert.Null(merged!.EndLocal);
        Assert.Null(merged.EndTimeZoneId);

        var sql = new SqlFileProvider().Get("Commands/EmailIngestion/MergeParsedItemDraftRecognition.sql");
        Assert.DoesNotContain("@EndLocal", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@EndTimeZoneId", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Recognition being unavailable still ends the draft's pending state, so a traveler who
    /// reopens it is not charged a second provider call for the same silence (FR-047).
    /// </summary>
    [Fact]
    public async Task AnUnavailableRecognitionStillSettlesTheState()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync();

        var merged = await _repository.MergeRecognitionAsync(draftId, userId, new DraftRecognitionMerge(
            null, null, null, null, null, null, null, null, null, null, null,
            DraftRecognitionStates.Unavailable));

        Assert.Equal(DraftRecognitionStates.Unavailable, merged!.TransportRecognitionState);
        Assert.Equal(DraftOutcomes.Item, merged.ProposedOutcome);
        Assert.Null(merged.Origin);
    }

    /// <summary>
    /// The top risk on this feature: re-examining a draft must never add one. `ReprocessAsync`
    /// inserts, which is why the re-recognition path may not reuse it.
    /// </summary>
    [Fact]
    public async Task MergingNeverAddsADraftToTheQueue()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync();

        await using var conn = await OpenAsync();
        var before = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM parsed_item_drafts WHERE user_id = @userId", new { userId });

        await _repository.MergeRecognitionAsync(draftId, userId, FullMerge());

        var after = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM parsed_item_drafts WHERE user_id = @userId", new { userId });

        Assert.Equal(before, after);
        Assert.Equal(1, after);
    }

    [Fact]
    public async Task AConfirmedDraftIsNotMerged()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync();

        await using (var conn = await OpenAsync())
        {
            await conn.ExecuteAsync(
                "UPDATE parsed_item_drafts SET review_status = 'confirmed' WHERE parsed_item_draft_id = @draftId",
                new { draftId });
        }

        var merged = await _repository.MergeRecognitionAsync(draftId, userId, FullMerge());

        Assert.Null(merged);
    }

    [Fact]
    public async Task AnotherTravelersDraftIsNotMerged()
    {
        var (draftId, _) = await SeedLegacyDraftAsync();

        var merged = await _repository.MergeRecognitionAsync(draftId, "someone-else", FullMerge());

        Assert.Null(merged);
    }

    /// <summary>
    /// FR-040: deleting the leg a draft produced must not put the draft back in the queue. The
    /// email still arrived and was still acted on, so the record of that survives the entity —
    /// the same reasoning `013_draft_item_traceability.sql` recorded for items.
    /// </summary>
    [Fact]
    public async Task DeletingTheCreatedLegLeavesTheDraftConfirmedAndOutOfTheQueue()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync(recognitionState: DraftRecognitionStates.Current);

        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();

        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
            VALUES (@tripId, @userId, 'Trip', DATE '2026-08-10', DATE '2026-08-20');

            INSERT INTO trip_legs (
                trip_leg_id, trip_id, owner_user_id, title, origin, destination,
                start_at, end_at, start_local, start_time_zone_id, end_local, end_time_zone_id,
                sort_order, leg_kind, transportation_mode)
            VALUES (
                @legId, @tripId, @userId, 'SEA to JFK', 'SEA', 'JFK',
                TIMESTAMPTZ '2026-08-12 09:30+00', TIMESTAMPTZ '2026-08-12 17:45+00',
                TIMESTAMP '2026-08-12 09:30', 'America/Los_Angeles',
                TIMESTAMP '2026-08-12 17:45', 'America/New_York',
                0, 'travel', 'flight');

            UPDATE parsed_item_drafts
            SET review_status = 'confirmed', proposed_outcome = 'leg', created_trip_leg_id = @legId
            WHERE parsed_item_draft_id = @draftId;
            """,
            new { tripId, legId, userId, draftId });

        await conn.ExecuteAsync("DELETE FROM trip_legs WHERE trip_leg_id = @legId", new { legId });

        var after = await conn.QuerySingleAsync<(string ReviewStatus, Guid? CreatedTripLegId)>(
            """
            SELECT review_status AS "ReviewStatus", created_trip_leg_id AS "CreatedTripLegId"
            FROM parsed_item_drafts WHERE parsed_item_draft_id = @draftId
            """,
            new { draftId });

        // ON DELETE SET NULL clears the pointer; the draft stays resolved.
        Assert.Equal("confirmed", after.ReviewStatus);
        Assert.Null(after.CreatedTripLegId);

        var pending = await conn.ExecuteScalarAsync<int>(
            """
            SELECT count(*) FROM parsed_item_drafts
            WHERE user_id = @userId AND review_status = 'pending_review'
            """,
            new { userId });
        Assert.Equal(0, pending);
    }

    /// <summary>
    /// Guards the defect that took a live database to find: Dapper could not materialize the
    /// <c>text[]</c> column through a positional record, so every draft read returned a 500.
    /// </summary>
    [Fact]
    public async Task ADraftReadsBackItsTravelerEditedFields()
    {
        var (draftId, userId) = await SeedLegacyDraftAsync(travelerEdited: ["origin", "travel_cost"]);

        var record = await _repository.GetByIdAsync(draftId, userId);

        Assert.NotNull(record);
        Assert.Equal(["origin", "travel_cost"], record!.TravelerEditedFields!.OrderBy(f => f).ToArray());
    }
}
