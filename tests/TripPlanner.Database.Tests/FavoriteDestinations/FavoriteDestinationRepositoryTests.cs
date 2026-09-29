using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;

namespace TripPlanner.Database.Tests.FavoriteDestinations;

[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public sealed class FavoriteDestinationRepositoryTests
{
    private readonly FavoriteDestinationRepository _favorites;

    public FavoriteDestinationRepositoryTests(PostgresFixture fixture)
        => _favorites = new FavoriteDestinationRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());

    private static string NewUser() => $"user-{Guid.NewGuid():N}";

    private static CreateFavoriteDestinationRequest Create(
        string name = "Museum Island",
        string address = "Bodestraße 1, Berlin",
        string? notes = "Allow a full afternoon")
        => new(name, address, notes);

    private static PlaceAddressComponents? Location(string? city = "Berlin", string? country = "Germany")
        => city is null && country is null ? null : new(city, country, null, null);

    private static readonly PlaceAddressComponents Berlin = new("Berlin", "Germany", 52.5169, 13.4019);

    [Fact]
    public async Task CreateAndRead_PersistsFavoriteOnlyForItsOwner()
    {
        var owner = NewUser();
        var otherOwner = NewUser();
        var created = await _favorites.CreateAsync(owner, Create(), Berlin, DateTimeOffset.UtcNow);

        var ownList = await _favorites.GetAllAsync(owner);
        var otherList = await _favorites.GetAllAsync(otherOwner);

        Assert.Equal(created, await _favorites.GetByIdAsync(owner, created.FavoriteDestinationId));
        Assert.Single(ownList, favorite => favorite.FavoriteDestinationId == created.FavoriteDestinationId);
        Assert.Empty(otherList);
        Assert.Null(await _favorites.GetByIdAsync(otherOwner, created.FavoriteDestinationId));
        Assert.Equal("Allow a full afternoon", created.Notes);
        Assert.Equal(("Berlin", "Germany", 52.5169, 13.4019), (created.City, created.Country, created.Latitude, created.Longitude));
    }

    [Fact]
    public async Task Update_IsOwnerScopedAndPersistsChanges()
    {
        var owner = NewUser();
        var otherOwner = NewUser();
        var created = await _favorites.CreateAsync(owner, Create(), Berlin, DateTimeOffset.UtcNow);
        var update = new UpdateFavoriteDestinationRequest(
            "Pergamon Museum", "Rue de Rivoli", "Book ahead");
        var paris = new PlaceAddressComponents("Paris", "France", 48.8606, 2.3376);

        Assert.Null(await _favorites.UpdateAsync(otherOwner, created.FavoriteDestinationId, update, paris, DateTimeOffset.UtcNow));
        var updated = await _favorites.UpdateAsync(owner, created.FavoriteDestinationId, update, paris, DateTimeOffset.UtcNow);
        var cleared = await _favorites.UpdateAsync(owner, created.FavoriteDestinationId, update, null, DateTimeOffset.UtcNow);

        Assert.NotNull(updated);
        Assert.Equal("Pergamon Museum", updated!.Name);
        Assert.Equal("Book ahead", updated.Notes);
        Assert.Equal(("Paris", "France", 48.8606, 2.3376), (updated.City, updated.Country, updated.Latitude, updated.Longitude));
        Assert.Equal((null, null, null, null), (cleared!.City, cleared.Country, cleared.Latitude, cleared.Longitude));
        Assert.Equal(cleared, await _favorites.GetByIdAsync(owner, created.FavoriteDestinationId));
    }

    [Fact]
    public async Task Delete_IsOwnerScopedAndDoesNotRevealOtherOwnersFavorite()
    {
        var owner = NewUser();
        var otherOwner = NewUser();
        var created = await _favorites.CreateAsync(owner, Create(), Location(), DateTimeOffset.UtcNow);

        Assert.False(await _favorites.DeleteAsync(otherOwner, created.FavoriteDestinationId));
        Assert.NotNull(await _favorites.GetByIdAsync(owner, created.FavoriteDestinationId));
        Assert.True(await _favorites.DeleteAsync(owner, created.FavoriteDestinationId));
        Assert.Null(await _favorites.GetByIdAsync(owner, created.FavoriteDestinationId));
        Assert.False(await _favorites.DeleteAsync(owner, created.FavoriteDestinationId));
    }

    [Fact]
    public async Task DeleteMany_IsOwnerScopedAndReturnsDeletedCount()
    {
        var owner = NewUser();
        var otherOwner = NewUser();
        var first = await _favorites.CreateAsync(owner, Create("First"), null, DateTimeOffset.UtcNow);
        var second = await _favorites.CreateAsync(owner, Create("Second"), null, DateTimeOffset.UtcNow);
        var kept = await _favorites.CreateAsync(owner, Create("Kept"), null, DateTimeOffset.UtcNow);
        var ids = new[] { first.FavoriteDestinationId, second.FavoriteDestinationId };

        Assert.Equal(0, await _favorites.DeleteManyAsync(otherOwner, ids));
        Assert.Equal(2, await _favorites.DeleteManyAsync(owner, ids));
        Assert.Equal(0, await _favorites.DeleteManyAsync(owner, ids));

        Assert.Equal(kept.FavoriteDestinationId, Assert.Single(await _favorites.GetAllAsync(owner)).FavoriteDestinationId);
    }

    [Fact]
    public async Task FindPossibleDuplicates_NormalizesNameAndAddressWithinOwner()
    {
        var owner = NewUser();
        var otherOwner = NewUser();
        await _favorites.CreateAsync(owner, Create(), Location(), DateTimeOffset.UtcNow);
        await _favorites.CreateAsync(otherOwner, Create(), Location(), DateTimeOffset.UtcNow);

        var matches = await _favorites.FindPossibleDuplicatesAsync(
            owner, "  MUSEUM ISLAND ", "bodestraße 1, BERLIN", null);
        var otherMatches = await _favorites.FindPossibleDuplicatesAsync(
            NewUser(), "Museum Island", "Bodestraße 1, Berlin", null);

        Assert.Single(matches);
        Assert.Empty(otherMatches);
    }

    [Fact]
    public async Task GetAll_SearchesEveryResearchFieldAndSortsBlankLocationsLast()
    {
        var owner = NewUser();
        var searchable = await _favorites.CreateAsync(owner, new CreateFavoriteDestinationRequest(
            "NameNeedle", "AddressNeedle", "NotesNeedle"), Location("CityNeedle", "CountryNeedle"), DateTimeOffset.UtcNow);
        await _favorites.CreateAsync(owner, Create("Zed", "Paris address"), Location("Paris", "France"), DateTimeOffset.UtcNow);
        await _favorites.CreateAsync(owner, Create("Alpha", "Paris address"), Location("Paris", "France"), DateTimeOffset.UtcNow);
        await _favorites.CreateAsync(owner, Create("Alpha", "Other Paris address"), Location("Paris", "France"), DateTimeOffset.UtcNow);
        await _favorites.CreateAsync(owner, Create("Berlin", "Berlin address"), Location("Berlin", "Germany"), DateTimeOffset.UtcNow);
        await _favorites.CreateAsync(owner, Create("No parsed location", "Free-form address"), Location(null, null), DateTimeOffset.UtcNow);

        foreach (var term in new[] { "nameneedle", "addressneedle", "CITYNEEDLE", "countryneedle", "notesneedle" })
        {
            var matches = await _favorites.GetAllAsync(owner, term);
            Assert.Equal(searchable.FavoriteDestinationId, Assert.Single(matches).FavoriteDestinationId);
        }

        var ordered = await _favorites.GetAllAsync(owner);
        Assert.Equal(["NameNeedle", "Alpha", "Alpha", "Zed", "Berlin", "No parsed location"], ordered.Select(favorite => favorite.Name));
        var tied = ordered.Where(favorite => favorite.Name == "Alpha").ToArray();
        Assert.Equal(tied.OrderBy(favorite => favorite.FavoriteDestinationId), tied);
    }
}