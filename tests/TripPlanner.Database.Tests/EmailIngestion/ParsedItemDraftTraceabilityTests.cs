using Dapper;
using Npgsql;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Database.Tests.EmailIngestion;

/// <summary>
/// Feature 024, FR-025: a confirmed draft points at the item it became. The script-shape checks
/// run everywhere and catch the likelier mistake of a column written but never read back; the
/// round trip below needs a live database and now has one.
/// </summary>
public class ParsedItemDraftTraceabilityTests
{
    [Fact]
    public void ReviewStatusCommandWritesTheTrackedItemId()
    {
        var sql = new SqlFileProvider().Get("Commands/EmailIngestion/UpdateParsedItemDraftReviewStatus.sql");
        Assert.Contains("tracked_item_id", sql);
        Assert.Contains("@TrackedItemId", sql);
    }

    // Dapper materializes a record by matching every constructor parameter to a column, so a
    // statement that returns a draft but omits this one fails at runtime rather than at build.
    [Theory]
    [InlineData("Commands/EmailIngestion/InsertParsedItemDraft.sql")]
    [InlineData("Commands/EmailIngestion/UpdateParsedItemDraft.sql")]
    [InlineData("Queries/EmailIngestion/GetParsedItemDrafts.sql")]
    [InlineData("Queries/EmailIngestion/GetParsedItemDraftById.sql")]
    public void EveryStatementThatReturnsADraftIncludesTheTrackedItemId(string script)
        => Assert.Contains("tracked_item_id AS TrackedItemId", new SqlFileProvider().Get(script));

    [Fact]
    public void TheTraceabilityColumnIsAddedIdempotently()
    {
        var sql = new SqlFileProvider().Get("Schema/013_draft_item_traceability.sql");
        Assert.Contains("ADD COLUMN IF NOT EXISTS tracked_item_id", sql);
    }
}

/// <summary>
/// The traceability round trip against a real database: confirming a draft records the item it
/// became, and deleting that item leaves the record of the email intact rather than resurrecting
/// the draft (FR-025, FR-040).
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class ParsedItemDraftTraceabilityRoundTripTests
{
    private readonly PostgresFixture _fixture;
    private readonly ParsedItemDraftRepository _drafts;

    public ParsedItemDraftTraceabilityRoundTripTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _drafts = new ParsedItemDraftRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    [Fact]
    public async Task ConfirmingADraftPersistsAndReadsBackTheTrackedItemId()
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var emailId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO inbox_emails (inbox_email_id, user_id, sender, subject, body_text, received_at, parse_status, dedupe_hash)
            VALUES (@emailId, @userId, 'a@b.c', 'Booking', 'body', now(), 'parsed', @hash);

            INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
            VALUES (@tripId, @userId, 'Trip', DATE '2026-08-10', DATE '2026-08-20');

            INSERT INTO tracked_items (tracked_item_id, trip_id, owner_user_id, item_type, title,
                                       starts_at, start_local, start_time_zone_id, display_color, sort_order)
            VALUES (@itemId, @tripId, @userId, 'reservation', 'Confirmed item',
                    TIMESTAMPTZ '2026-08-12 09:30+00', TIMESTAMP '2026-08-12 09:30', 'UTC', 'slate', 0);
            """,
            new { emailId, userId, tripId, itemId, hash = Guid.NewGuid().ToString() });

        var draft = await _drafts.InsertAsync(new NewParsedItemDraft(
            emailId, userId, tripId, null, "reservation", "Confirmed item", null,
            new DateTime(2026, 8, 12, 9, 30, 0), "UTC", null, null, "ABC123", null, 0.9));

        var confirmed = await _drafts.SetReviewStatusAsync(
            draft!.ParsedItemDraftId, userId, "confirmed", itemId);
        Assert.True(confirmed);

        var read = await _drafts.GetByIdAsync(draft.ParsedItemDraftId, userId);
        Assert.Equal("confirmed", read!.ReviewStatus);
        Assert.Equal(itemId, read.TrackedItemId);

        // The draft is resolved, so it is gone from the pending queue.
        Assert.DoesNotContain(await _drafts.GetPendingAsync(userId), d => d.ParsedItemDraftId == draft.ParsedItemDraftId);
    }

    /// <summary>
    /// Deleting the item nulls the pointer but leaves the draft confirmed — the email still
    /// arrived and was still acted on, and that record outlives the entity (FR-040).
    /// </summary>
    [Fact]
    public async Task DeletingTheTrackedItemLeavesTheDraftConfirmed()
    {
        var userId = $"user-{Guid.NewGuid():N}";
        var emailId = Guid.NewGuid();
        var tripId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO inbox_emails (inbox_email_id, user_id, sender, subject, body_text, received_at, parse_status, dedupe_hash)
            VALUES (@emailId, @userId, 'a@b.c', 'Booking', 'body', now(), 'parsed', @hash);

            INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
            VALUES (@tripId, @userId, 'Trip', DATE '2026-08-10', DATE '2026-08-20');

            INSERT INTO tracked_items (tracked_item_id, trip_id, owner_user_id, item_type, title,
                                       starts_at, start_local, start_time_zone_id, display_color, sort_order)
            VALUES (@itemId, @tripId, @userId, 'reservation', 'Doomed item',
                    TIMESTAMPTZ '2026-08-12 09:30+00', TIMESTAMP '2026-08-12 09:30', 'UTC', 'slate', 0);
            """,
            new { emailId, userId, tripId, itemId, hash = Guid.NewGuid().ToString() });

        var draft = await _drafts.InsertAsync(new NewParsedItemDraft(
            emailId, userId, tripId, null, "reservation", "Doomed item", null,
            new DateTime(2026, 8, 12, 9, 30, 0), "UTC", null, null, null, null, 0.9));

        await _drafts.SetReviewStatusAsync(draft!.ParsedItemDraftId, userId, "confirmed", itemId);

        await conn.ExecuteAsync("DELETE FROM tracked_items WHERE tracked_item_id = @itemId", new { itemId });

        var read = await _drafts.GetByIdAsync(draft.ParsedItemDraftId, userId);
        Assert.Equal("confirmed", read!.ReviewStatus);
        Assert.Null(read.TrackedItemId);
        Assert.DoesNotContain(await _drafts.GetPendingAsync(userId), d => d.ParsedItemDraftId == draft.ParsedItemDraftId);
    }
}
