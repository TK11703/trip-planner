using TripPlanner.Database.Sql;
using Xunit;

namespace TripPlanner.Database.Tests.EmailIngestion;

/// <summary>
/// Feature 024, FR-025: a confirmed draft points at the item it became. The full round trip needs
/// a live database, so the container-backed case is declared here alongside the rest of the
/// integration suite; the script-shape checks below run everywhere and catch the far more likely
/// mistake of a column that is written but never read back.
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

    [Trait("Category", "DatabaseIntegration")]
    [Fact(Skip = "Requires Docker/Testcontainers; enable in environments with container runtime.")]
    public void ConfirmingADraftPersistsAndReadsBackTheTrackedItemId() { }
}
