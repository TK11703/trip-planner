using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;

namespace TripPlanner.Database.Tests.FavoriteDestinations;

[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public sealed class FavoriteDestinationImportTransactionTests
{
    private readonly FavoriteDestinationRepository _favorites;

    public FavoriteDestinationImportTransactionTests(PostgresFixture fixture)
        => _favorites = new FavoriteDestinationRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());

    [Fact]
    public async Task CreateManyAsync_InsertsTheWholeBatchForOneOwner()
    {
        var owner = $"user-{Guid.NewGuid():N}";
        var requests = new (CreateFavoriteDestinationRequest, PlaceAddressComponents?)[]
        {
            (new CreateFavoriteDestinationRequest("Museum", "Berlin", null), new PlaceAddressComponents("Berlin", "Germany", 52.5169, 13.4019)),
            (new CreateFavoriteDestinationRequest("Garden", "Paris", "Visit early"), null)
        };

        var inserted = await _favorites.CreateManyAsync(owner, requests, DateTimeOffset.UtcNow);
        var saved = await _favorites.GetAllAsync(owner);

        Assert.Equal(2, inserted.Count);
        Assert.All(inserted, favorite => Assert.Contains(saved, item => item.FavoriteDestinationId == favorite.FavoriteDestinationId));
        var museum = Assert.Single(inserted, favorite => favorite.Name == "Museum");
        var garden = Assert.Single(inserted, favorite => favorite.Name == "Garden");
        Assert.Equal(("Berlin", "Germany", 52.5169, 13.4019), (museum.City, museum.Country, museum.Latitude, museum.Longitude));
        Assert.Equal((null, null, null, null), (garden.City, garden.Country, garden.Latitude, garden.Longitude));
        Assert.Empty(await _favorites.GetAllAsync("other-user"));
    }

    [Fact]
    public async Task CreateManyAsync_RollsBackEarlierRowsWhenAnyInsertFails()
    {
        var owner = $"user-{Guid.NewGuid():N}";
        var requests = new (CreateFavoriteDestinationRequest, PlaceAddressComponents?)[]
        {
            (new CreateFavoriteDestinationRequest("Museum", "Berlin", null), null),
            (new CreateFavoriteDestinationRequest("   ", "Paris", null), null)
        };

        await Assert.ThrowsAnyAsync<Exception>(() => _favorites.CreateManyAsync(owner, requests, DateTimeOffset.UtcNow));

        Assert.Empty(await _favorites.GetAllAsync(owner));
    }
}