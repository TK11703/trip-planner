using Dapper;
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
