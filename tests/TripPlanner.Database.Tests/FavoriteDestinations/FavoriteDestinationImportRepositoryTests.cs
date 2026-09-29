using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;

namespace TripPlanner.Database.Tests.FavoriteDestinations;

[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public sealed class FavoriteDestinationImportRepositoryTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private readonly FavoriteDestinationImportRepository _imports;
    private readonly FavoriteDestinationRepository _favorites;

    public FavoriteDestinationImportRepositoryTests(PostgresFixture fixture)
    {
        var connections = new StubConnectionFactory(fixture.ConnectionString);
        _imports = new FavoriteDestinationImportRepository(connections, new SqlFileProvider());
        _favorites = new FavoriteDestinationRepository(connections, new SqlFileProvider());
    }

    [Fact]
    public async Task ImportLifecycle_StagesRowsAndCompletesAtomicallyForItsOwner()
    {
        var owner = $"user-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var created = await _imports.CreateAsync(owner, "saved.csv",
        [
            new FavoriteDestinationImportRowInput(1, "Museum", "Berlin", "Visit"),
            new FavoriteDestinationImportRowInput(2, "Blue Bottle Coffee", "https://maps.app.goo.gl/abc", null)
        ], now);
        var claimed = await ClaimAsync(created.ImportId, now);
        var candidates = new[]
        {
            new FavoriteDestinationPlaceCandidate("Blue Bottle Coffee", "300 Webster St, Oakland", "Oakland", "United States", 37.8, -122.27),
            new FavoriteDestinationPlaceCandidate("Blue Bottle Coffee", "66 Mint St, San Francisco", "San Francisco", "United States", 37.78, -122.41)
        };

        Assert.True(await _imports.SaveRowResolutionAsync(created.ImportId, new FavoriteDestinationImportRowResolution(
            1, FavoriteDestinationImportRowStatus.Resolved, false, "Berlin", new PlaceAddressComponents("Berlin", "Germany", 52.52, 13.4, "Berlin"), []), now, now + Lease));
        var inProgress = await _imports.GetAsync(owner, created.ImportId);
        Assert.True(await _imports.SaveRowResolutionAsync(created.ImportId, new FavoriteDestinationImportRowResolution(
            2, FavoriteDestinationImportRowStatus.Ambiguous, true, null, null, candidates), now, now + Lease));
        Assert.True(await _imports.MarkNeedsReviewAsync(created.ImportId, [1], now));
        var review = await _imports.GetAsync(owner, created.ImportId);
        var rows = await _imports.GetRowsAsync(owner, created.ImportId);

        Assert.Equal((FavoriteDestinationImportStatus.Processing, 1), (claimed.Status, claimed.AttemptCount));
        Assert.Equal((1, 2), (inProgress!.ProcessedRows, inProgress.TotalRows));
        Assert.Equal(FavoriteDestinationImportStatus.NeedsReview, review!.Status);
        Assert.Null(await _imports.GetAsync("someone-else", created.ImportId));
        Assert.Empty(await _imports.GetRowsAsync("someone-else", created.ImportId));
        Assert.Equal(("Germany", true), (rows[0].Location?.Country, rows[0].IsPossibleDuplicate));
        Assert.Equal((FavoriteDestinationImportRowStatus.Ambiguous, true, false), (rows[1].Status, rows[1].UsesResolvedAddress, rows[1].IsPossibleDuplicate));
        Assert.Equal(candidates, rows[1].Candidates.ToArray());
        Assert.Single(await _imports.GetOpenAsync(owner));

        var inserted = await _imports.CompleteAsync(owner, created.ImportId, FavoriteDestinationImportStatus.NeedsReview,
        [
            (new CreateFavoriteDestinationRequest("Museum", "Berlin", "Visit"), rows[0].Location),
            (new CreateFavoriteDestinationRequest("Blue Bottle Coffee", "66 Mint St, San Francisco", null), new PlaceAddressComponents("San Francisco", "United States", 37.78, -122.41))
        ], now);
        var repeated = await _imports.CompleteAsync(owner, created.ImportId, FavoriteDestinationImportStatus.NeedsReview, [], now);
        var completed = await _imports.GetAsync(owner, created.ImportId);

        Assert.Equal(2, inserted!.Count);
        Assert.Null(repeated);
        Assert.Equal((FavoriteDestinationImportStatus.Completed, 2, 2), (completed!.Status, completed.ImportedCount, completed.ProcessedRows));
        Assert.Empty(await _imports.GetRowsAsync(owner, created.ImportId));
        Assert.Empty(await _imports.GetOpenAsync(owner));
        Assert.Equal(2, (await _favorites.GetAllAsync(owner)).Count);
    }

    [Fact]
    public async Task CompleteAsync_RollsBackTheStatusWhenAFavoriteInsertFails()
    {
        var owner = $"user-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var created = await _imports.CreateAsync(owner, "bad.json", [new FavoriteDestinationImportRowInput(1, "Museum", "Berlin", null)], now);
        await ClaimAsync(created.ImportId, now);

        await Assert.ThrowsAnyAsync<Exception>(() => _imports.CompleteAsync(owner, created.ImportId, FavoriteDestinationImportStatus.Processing,
            [(new CreateFavoriteDestinationRequest("   ", "Berlin", null), null)], now));

        Assert.Equal(FavoriteDestinationImportStatus.Processing, (await _imports.GetAsync(owner, created.ImportId))!.Status);
        Assert.Empty(await _favorites.GetAllAsync(owner));
    }

    [Fact]
    public async Task DeleteAsync_IsOwnerScopedAndStopsProcessing()
    {
        var owner = $"user-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var created = await _imports.CreateAsync(owner, "saved.json", [new FavoriteDestinationImportRowInput(1, "Museum", "Berlin", null)], now);
        await ClaimAsync(created.ImportId, now);

        Assert.False(await _imports.DeleteAsync("someone-else", created.ImportId));
        Assert.True(await _imports.DeleteAsync(owner, created.ImportId));
        Assert.False(await _imports.SaveRowResolutionAsync(created.ImportId, new FavoriteDestinationImportRowResolution(
            1, FavoriteDestinationImportRowStatus.Resolved, false, "Berlin", null, []), now, now + Lease));
        Assert.Null(await _imports.GetAsync(owner, created.ImportId));
    }

    [Fact]
    public async Task ClaimNextAsync_ReclaimsOnlyExpiredLeases()
    {
        var owner = $"user-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var created = await _imports.CreateAsync(owner, "saved.json", [new FavoriteDestinationImportRowInput(1, "Museum", "Berlin", null)], now);
        await ClaimAsync(created.ImportId, now);

        var whileLeased = await DrainClaimsAsync(now.AddMinutes(1));
        var afterExpiry = await DrainClaimsAsync(now + Lease + TimeSpan.FromMinutes(1));

        Assert.DoesNotContain(whileLeased, job => job.ImportId == created.ImportId);
        var reclaimed = Assert.Single(afterExpiry, job => job.ImportId == created.ImportId);
        Assert.Equal(2, reclaimed.AttemptCount);
        await _imports.MarkFailedAsync(created.ImportId, "Failed for the test.", now);
        Assert.Equal("Failed for the test.", (await _imports.GetAsync(owner, created.ImportId))!.ErrorMessage);
    }

    // Other tests in the shared database may leave queued imports, so claim until ours comes up.
    private async Task<FavoriteDestinationImportJob> ClaimAsync(Guid importId, DateTimeOffset now)
    {
        var claimed = await DrainClaimsAsync(now);
        return Assert.Single(claimed, job => job.ImportId == importId);
    }

    private async Task<IReadOnlyList<FavoriteDestinationImportJob>> DrainClaimsAsync(DateTimeOffset now)
    {
        var claimed = new List<FavoriteDestinationImportJob>();
        for (var i = 0; i < 100; i++)
        {
            var job = await _imports.ClaimNextAsync(now, now + Lease);
            if (job is null)
            {
                break;
            }
            claimed.Add(job);
        }
        return claimed;
    }
}
