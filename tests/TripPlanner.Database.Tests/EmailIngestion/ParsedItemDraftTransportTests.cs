using Dapper;
using System.Globalization;
using Npgsql;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Database.Tests.EmailIngestion;

/// <summary>
/// The transport columns `016` adds, checked against a real PostgreSQL instance: a draft carries
/// its route through a full round trip, and the database refuses a value the domain does not
/// allow even if a future caller stops checking (FR-008, FR-010, FR-042, FR-045).
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class ParsedItemDraftTransportTests
{
    private readonly PostgresFixture _fixture;
    private readonly ParsedItemDraftRepository _repository;

    public ParsedItemDraftTransportTests(PostgresFixture fixture)
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

    private async Task<(Guid EmailId, string UserId)> SeedEmailAsync()
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var emailId = Guid.NewGuid();

        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO inbox_emails (inbox_email_id, user_id, sender, subject, body_text, received_at, parse_status, dedupe_hash)
            VALUES (@emailId, @userId, 'a@b.c', 'Booking', 'body', now(), 'parsed', @hash);
            """,
            new { emailId, userId, hash = Guid.NewGuid().ToString() });

        return (emailId, userId);
    }

    [Fact]
    public async Task ATransportDraftRoundTripsEveryNewColumn()
    {
        var (emailId, userId) = await SeedEmailAsync();

        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "flight", "Flight ABC123", "SEA",
            new DateTime(2026, 8, 12, 9, 30, 0), "America/Los_Angeles",
            new DateTime(2026, 8, 12, 17, 45, 0), "America/New_York",
            "ABC123", null, 0.94,
            DraftOutcomes.Leg, "SEA", "JFK", "flight", 412.50m, "USD"));

        // Read back through the query the review queue actually uses, not the insert's own
        // RETURNING clause — a column written but never projected is the likelier mistake.
        var read = await _repository.GetByIdAsync(inserted!.ParsedItemDraftId, userId);

        Assert.NotNull(read);
        Assert.Equal(DraftOutcomes.Leg, read!.ProposedOutcome);
        Assert.Equal("SEA", read.Origin);
        Assert.Equal("JFK", read.Destination);
        Assert.Equal("flight", read.TransportationMode);
        Assert.Equal(412.50m, read.TravelCost);
        Assert.Equal("USD", read.TravelCostCurrency);
        Assert.Null(read.CreatedTripLegId);
        Assert.Empty(read.TravelerEditedFields!);
    }

    [Fact]
    public async Task ANonTransportDraftDefaultsToTheItemOutcome()
    {
        var (emailId, userId) = await SeedEmailAsync();

        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "hotel", "Hotel", "Seattle",
            new DateTime(2026, 8, 12, 15, 0, 0), "America/Los_Angeles", null, null,
            "HTL1", null, 0.9));

        Assert.Equal(DraftOutcomes.Item, inserted!.ProposedOutcome);
        Assert.Null(inserted.Origin);
        Assert.Null(inserted.TransportationMode);
        Assert.Null(inserted.EstimatedCost);
        Assert.Null(inserted.LegKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("travel")]
    [InlineData("stay")]
    public async Task DraftLegKindRoundTripsAndSurvivesRecognition(string? kind)
    {
        var (emailId, userId) = await SeedEmailAsync();
        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "hotel", "Seattle", null,
            null, null, null, null, null, null, 0.9, LegKind: kind));
        Assert.Equal(kind, inserted!.LegKind);
        Assert.Equal(kind, (await _repository.GetByIdAsync(inserted.ParsedItemDraftId, userId))!.LegKind);
        Assert.Equal(kind, Assert.Single(await _repository.GetPendingAsync(userId)).LegKind);

        var update = new DraftUpdate(null, null, "hotel", "Seattle", null,
            null, null, null, null, null, null, DraftOutcomes.Leg, LegKind: "stay");
        Assert.Null(await _repository.UpdateAsync(inserted.ParsedItemDraftId, "another-owner", update));
        Assert.Equal(kind, (await _repository.GetByIdAsync(inserted.ParsedItemDraftId, userId))!.LegKind);
        var updated = await _repository.UpdateAsync(inserted.ParsedItemDraftId, userId, update);
        Assert.Equal("stay", updated!.LegKind);
        Assert.Equal("stay", (await _repository.GetByIdAsync(inserted.ParsedItemDraftId, userId))!.LegKind);
        Assert.Equal("stay", Assert.Single(await _repository.GetPendingAsync(userId)).LegKind);

        var merged = await _repository.MergeRecognitionAsync(inserted.ParsedItemDraftId, userId,
            new DraftRecognitionMerge(DraftOutcomes.Leg, "SEA", "JFK", "flight", null, null,
                null, null, null, null, null, DraftRecognitionStates.Current));
        Assert.Equal("stay", merged!.LegKind);
        Assert.Equal("flight", merged.TransportationMode);

        var changed = await _repository.UpdateAsync(inserted.ParsedItemDraftId, userId, update with { LegKind = "travel" });
        Assert.Equal("travel", changed!.LegKind);
        await _repository.SetReviewStatusAsync(inserted.ParsedItemDraftId, userId, "discarded");
        Assert.Null(await _repository.UpdateAsync(inserted.ParsedItemDraftId, userId, update));
    }

    [Fact]
    public async Task TheDatabaseRejectsAnUnknownDraftLegKind()
    {
        var (emailId, userId) = await SeedEmailAsync();
        var exception = await Assert.ThrowsAsync<PostgresException>(() => _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "hotel", "Seattle", null,
            null, null, null, null, null, null, 0.9, LegKind: "flight")));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("123.45")]
    [InlineData("9999999999.99")]
    public async Task ItemEstimateRoundTripsIndependentlyOfTravelCostAndCanBeCleared(string? amount)
    {
        var expected = amount is null ? (decimal?)null : decimal.Parse(amount, CultureInfo.InvariantCulture);
        var (emailId, userId) = await SeedEmailAsync();
        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "activity", "Museum", null,
            null, null, null, null, "ABC123", null, 0.9,
            TravelCost: 412.50m, EstimatedCost: expected));
        Assert.Equal(expected, inserted!.EstimatedCost);

        var read = await _repository.GetByIdAsync(inserted.ParsedItemDraftId, userId);
        Assert.Equal(expected, read!.EstimatedCost);
        Assert.Equal(expected, Assert.Single(await _repository.GetPendingAsync(userId)).EstimatedCost);
        Assert.Equal(412.50m, read.TravelCost);

        var update = new DraftUpdate(null, null, "activity", "Museum", null,
            null, null, null, null, "ABC123", null, TravelCost: 412.50m, EstimatedCost: 25.75m);
        var updated = await _repository.UpdateAsync(inserted.ParsedItemDraftId, userId, update);
        Assert.Equal(25.75m, updated!.EstimatedCost);

        var merged = await _repository.MergeRecognitionAsync(inserted.ParsedItemDraftId, userId,
            new DraftRecognitionMerge(null, null, null, null, null, null, null, null, null, null, null,
                DraftRecognitionStates.Current));
        Assert.Equal(25.75m, merged!.EstimatedCost);

        var cleared = await _repository.UpdateAsync(inserted.ParsedItemDraftId, userId, update with { EstimatedCost = null });
        Assert.Null(cleared!.EstimatedCost);
        Assert.Null((await _repository.GetByIdAsync(inserted.ParsedItemDraftId, userId))!.EstimatedCost);
        Assert.Equal(412.50m, cleared.TravelCost);
    }

    [Fact]
    public async Task AnotherOwnerCannotReadOrChangeAnEstimateAndResolvedDraftsCannotBeEdited()
    {
        var (emailId, userId) = await SeedEmailAsync();
        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "activity", "Museum", null,
            null, null, null, null, null, null, 0.9, EstimatedCost: 25.75m));
        var update = new DraftUpdate(null, null, "activity", "Museum", null,
            null, null, null, null, null, null, EstimatedCost: 100m);

        Assert.Null(await _repository.GetByIdAsync(inserted!.ParsedItemDraftId, "another-owner"));
        Assert.Null(await _repository.UpdateAsync(inserted.ParsedItemDraftId, "another-owner", update));
        Assert.Equal(25.75m, (await _repository.GetByIdAsync(inserted.ParsedItemDraftId, userId))!.EstimatedCost);
        await _repository.SetReviewStatusAsync(inserted.ParsedItemDraftId, userId, "discarded");
        Assert.Null(await _repository.UpdateAsync(inserted.ParsedItemDraftId, userId, update));
    }

    [Fact]
    public async Task TheDatabaseRefusesANegativeEstimatedCost()
    {
        var (emailId, userId) = await SeedEmailAsync();
        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "activity", "Museum", null,
            null, null, null, null, null, null, 0.9));
        await using var conn = await OpenAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE parsed_item_drafts SET estimated_cost = @cost WHERE parsed_item_draft_id = @id",
            new { cost = -1m, id = inserted!.ParsedItemDraftId }));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    /// <summary>
    /// A draft created after the migration is already current: only rows that predate the column
    /// are marked pending. The alternative — a guarded backfill keyed on review status — would
    /// also match new drafts and charge each one a needless recognition call (FR-045).
    /// </summary>
    [Fact]
    public async Task ADraftCreatedNowIsAlreadyCurrent()
    {
        var (emailId, userId) = await SeedEmailAsync();

        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "flight", "Flight", null,
            null, null, null, null, null, null, 0.9));

        Assert.Equal(DraftRecognitionStates.Current, inserted!.TransportRecognitionState);
    }

    [Theory]
    [InlineData("proposed_outcome", "'banana'")]
    [InlineData("transportation_mode", "'spaceship'")]
    [InlineData("transport_recognition_state", "'bogus'")]
    public async Task TheDatabaseRefusesAValueOutsideItsAllowedSet(string column, string literal)
    {
        var (emailId, userId) = await SeedEmailAsync();
        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "flight", "Flight", null, null, null, null, null, null, null, 0.9));

        await using var conn = await OpenAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => conn.ExecuteAsync(
            $"UPDATE parsed_item_drafts SET {column} = {literal} WHERE parsed_item_draft_id = @id",
            new { id = inserted!.ParsedItemDraftId }));

        Assert.Equal("23514", ex.SqlState); // check_violation
    }

    [Fact]
    public async Task TheDatabaseRefusesANegativeTravelCost()
    {
        var (emailId, userId) = await SeedEmailAsync();
        var inserted = await _repository.InsertAsync(new NewParsedItemDraft(
            emailId, userId, null, null, "flight", "Flight", null, null, null, null, null, null, null, 0.9));

        await using var conn = await OpenAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE parsed_item_drafts SET travel_cost = -1 WHERE parsed_item_draft_id = @id",
            new { id = inserted!.ParsedItemDraftId }));

        Assert.Equal("23514", ex.SqlState);
    }

    /// <summary>
    /// The migration names neither timeline table in a write. Nothing already confirmed may be
    /// converted, moved, or deleted (FR-043, FR-048).
    /// </summary>
    [Fact]
    public void TheMigrationWritesToNeitherTimelineTable()
    {
        var sql = new SqlFileProvider().Get("Schema/016_draft_transport_outcome.sql");

        foreach (var statement in new[] { "INSERT INTO tracked_items", "UPDATE tracked_items", "DELETE FROM tracked_items",
                                          "INSERT INTO trip_legs", "UPDATE trip_legs", "DELETE FROM trip_legs" })
        {
            Assert.DoesNotContain(statement, sql, StringComparison.OrdinalIgnoreCase);
        }

        // The one permitted mention: the traceability foreign key FR-040 depends on.
        Assert.Contains("REFERENCES trip_legs", sql, StringComparison.Ordinal);
    }
}
