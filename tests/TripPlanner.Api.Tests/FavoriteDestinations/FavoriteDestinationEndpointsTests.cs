using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Api.Features.Places;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;
using TripPlanner.Database.FavoriteDestinations;

namespace TripPlanner.Api.Tests.FavoriteDestinations;

public sealed class FavoriteDestinationEndpointsTests
{
    [Fact]
    public async Task AnonymousRequests_AreUnauthorized()
    {
        await using var factory = new FavoritesApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/favorite-destinations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndList_UseAuthenticatedOwnerAndKeepFavoritesPrivate()
    {
        await using var factory = new FavoritesApiFactory();
        using var ownerClient = factory.CreateClient();
        using var otherClient = factory.CreateClient();
        ownerClient.AddUser("owner-a");
        otherClient.AddUser("owner-b");

        var created = await ownerClient.PostAsJsonAsync("/api/favorite-destinations", Request());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var favorite = await created.Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        Assert.NotNull(favorite);
        Assert.Null(typeof(CreateFavoriteDestinationRequest).GetProperty("OwnerUserId"));
        Assert.Equal("owner-a", factory.Repository.OwnerOf(favorite!.FavoriteDestinationId));

        var ownerList = await ownerClient.GetFromJsonAsync<FavoriteDestinationDto[]>("/api/favorite-destinations");
        var otherList = await otherClient.GetFromJsonAsync<FavoriteDestinationDto[]>("/api/favorite-destinations");

        Assert.Contains(ownerList!, item => item.FavoriteDestinationId == favorite.FavoriteDestinationId);
        Assert.Empty(otherList!);
    }

    [Fact]
    public async Task Create_ReportsRequiredNameAndAddressErrors()
    {
        await using var factory = new FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-a");

        var response = await client.PostAsJsonAsync("/api/favorite-destinations", Request(name: " ", address: null));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("name", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("address", body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(factory.Repository.All);
    }

    [Fact]
    public async Task Create_RequiresExplicitPossibleDuplicateConfirmation()
    {
        await using var factory = new FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-a");

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/favorite-destinations", Request())).StatusCode);
        var unconfirmed = await client.PostAsJsonAsync("/api/favorite-destinations", Request(name: " museum island ", address: "Bodestraße 1, Berlin"));
        var duplicateWarning = await unconfirmed.Content.ReadFromJsonAsync<FavoriteDestinationDuplicateWarning>();
        var confirmed = await client.PostAsJsonAsync("/api/favorite-destinations", Request(confirmPossibleDuplicate: true));

        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        Assert.NotNull(duplicateWarning);
        Assert.Single(duplicateWarning!.Matches);
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);
        Assert.Equal(2, factory.Repository.All.Count);
    }

    [Fact]
    public async Task Create_UsesResolvedComponentsAndStillSavesWhenLookupIsUnavailable()
    {
        await using var factory = new FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-a");
        factory.Lookup.Resolution = new PlaceAddressComponents("Paris", "France", 48.8606, 2.3376);

        var resolvedResponse = await client.PostAsJsonAsync("/api/favorite-destinations", Request("Louvre", "Rue de Rivoli"));
        var resolved = await resolvedResponse.Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        factory.Lookup.Resolution = null;
        factory.Lookup.ThrowOnResolve = true;

        var fallbackResponse = await client.PostAsJsonAsync("/api/favorite-destinations", Request("Unmapped", "Free-form address"));
        var fallback = await fallbackResponse.Content.ReadFromJsonAsync<FavoriteDestinationDto>();

        Assert.Equal("Rue de Rivoli", resolved!.Address);
        Assert.Equal("Paris", resolved.City);
        Assert.Equal("France", resolved.Country);
        Assert.Equal(48.8606, resolved.Latitude);
        Assert.Equal(2.3376, resolved.Longitude);
        Assert.Equal(HttpStatusCode.Created, fallbackResponse.StatusCode);
        Assert.Equal("Free-form address", fallback!.Address);
        Assert.Null(fallback.City);
        Assert.Null(fallback.Country);
        Assert.Null(fallback.Latitude);
        Assert.Null(fallback.Longitude);
    }

    [Fact]
    public async Task Update_WithUnchangedAddress_KeepsCalculatedLocationWithoutLookup()
    {
        await using var factory = new FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-a");
        factory.Lookup.Resolution = new PlaceAddressComponents("Berlin", "Germany", 52.5169, 13.4019);
        var created = await (await client.PostAsJsonAsync("/api/favorite-destinations", Request()))
            .Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        var lookupsAfterCreate = factory.Lookup.ResolveCallCount;

        var response = await client.PutAsJsonAsync(
            $"/api/favorite-destinations/{created!.FavoriteDestinationId}",
            new UpdateFavoriteDestinationRequest("Renamed", " Bodestraße 1, Berlin ", "New notes"));
        var updated = await response.Content.ReadFromJsonAsync<FavoriteDestinationDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(lookupsAfterCreate, factory.Lookup.ResolveCallCount);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Equal("Berlin", updated.City);
        Assert.Equal("Germany", updated.Country);
        Assert.Equal(52.5169, updated.Latitude);
        Assert.Equal(13.4019, updated.Longitude);
    }

    [Fact]
    public async Task Update_WithChangedAddress_ClearsAndRecalculatesLocation()
    {
        await using var factory = new FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-a");
        factory.Lookup.Resolution = new PlaceAddressComponents("Berlin", "Germany", 52.5169, 13.4019);
        var created = await (await client.PostAsJsonAsync("/api/favorite-destinations", Request()))
            .Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        var url = $"/api/favorite-destinations/{created!.FavoriteDestinationId}";

        factory.Lookup.Resolution = new PlaceAddressComponents("Paris", "France", 48.8606, 2.3376);
        var moved = await (await client.PutAsJsonAsync(url, new UpdateFavoriteDestinationRequest("Museum", "Rue de Rivoli", null)))
            .Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        factory.Lookup.ThrowOnResolve = true;
        var unresolved = await (await client.PutAsJsonAsync(url, new UpdateFavoriteDestinationRequest("Museum", "Somewhere unmapped", null)))
            .Content.ReadFromJsonAsync<FavoriteDestinationDto>();

        Assert.Equal(("Paris", "France", 48.8606, 2.3376), (moved!.City, moved.Country, moved.Latitude, moved.Longitude));
        Assert.Equal("Somewhere unmapped", unresolved!.Address);
        Assert.Null(unresolved.City);
        Assert.Null(unresolved.Country);
        Assert.Null(unresolved.Latitude);
        Assert.Null(unresolved.Longitude);
    }

    [Fact]
    public void Requests_DoNotAcceptCallerSuppliedLocation()
    {
        foreach (var type in new[] { typeof(CreateFavoriteDestinationRequest), typeof(UpdateFavoriteDestinationRequest) })
        {
            Assert.Null(type.GetProperty("City"));
            Assert.Null(type.GetProperty("Country"));
            Assert.Null(type.GetProperty("Latitude"));
            Assert.Null(type.GetProperty("Longitude"));
        }
    }

    private static CreateFavoriteDestinationRequest Request(
        string? name = "Museum Island",
        string? address = "Bodestraße 1, Berlin",
        bool confirmPossibleDuplicate = false)
        => new(name, address, "Allow a full afternoon", confirmPossibleDuplicate);

    internal sealed class FavoritesApiFactory : TestApiFactory
    {
        public InMemoryFavoriteDestinationRepository Repository { get; } = new();
        public FakePlaceLookup Lookup { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFavoriteDestinationRepository>();
                services.AddSingleton<IFavoriteDestinationRepository>(Repository);
                services.RemoveAll<IPlaceSuggestionLookup>();
                services.AddSingleton<IPlaceSuggestionLookup>(Lookup);
            });
        }
    }

    internal sealed class FakePlaceLookup : IPlaceSuggestionLookup
    {
        public bool IsConfigured => true;
        public PlaceAddressComponents? Resolution { get; set; }
        public Dictionary<string, PlaceAddressComponents> ResolutionsByAddress { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool ThrowOnResolve { get; set; }
        public int ResolveCallCount { get; private set; }

        public Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(string query, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlaceSuggestion>>(Array.Empty<PlaceSuggestion>());

        public Task<PlaceAddressComponents?> ResolveAddressAsync(string query, CancellationToken ct)
        {
            ResolveCallCount++;
            return ThrowOnResolve
                ? throw new HttpRequestException("Maps unavailable")
                : Task.FromResult(ResolutionsByAddress.GetValueOrDefault(query) ?? Resolution);
        }
    }

    internal sealed class InMemoryFavoriteDestinationRepository : IFavoriteDestinationRepository
    {
        private readonly List<(string Owner, FavoriteDestinationDto Favorite)> _favorites = [];
        public IReadOnlyList<(string Owner, FavoriteDestinationDto Favorite)> All => _favorites;

        public string? OwnerOf(Guid id) => _favorites.FirstOrDefault(item => item.Favorite.FavoriteDestinationId == id).Owner;

        public Task<IReadOnlyList<FavoriteDestinationDto>> GetAllAsync(string ownerUserId, string? search = null, CancellationToken cancellationToken = default)
        {
            var matches = _favorites
                .Where(item => item.Owner == ownerUserId)
                .Select(item => item.Favorite)
                .Where(favorite => string.IsNullOrWhiteSpace(search)
                    || new[] { favorite.Name, favorite.Address, favorite.City, favorite.Country, favorite.Notes }
                        .Any(value => value?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true))
                .OrderBy(favorite => string.IsNullOrWhiteSpace(favorite.Country) ? 1 : 0)
                .ThenBy(favorite => favorite.Country, StringComparer.OrdinalIgnoreCase)
                .ThenBy(favorite => string.IsNullOrWhiteSpace(favorite.City) ? 1 : 0)
                .ThenBy(favorite => favorite.City, StringComparer.OrdinalIgnoreCase)
                .ThenBy(favorite => favorite.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(favorite => favorite.FavoriteDestinationId)
                .ToArray();
            return Task.FromResult<IReadOnlyList<FavoriteDestinationDto>>(matches);
        }

        public Task<FavoriteDestinationDto?> GetByIdAsync(string ownerUserId, Guid favoriteDestinationId, CancellationToken cancellationToken = default)
            => Task.FromResult(_favorites.FirstOrDefault(item => item.Owner == ownerUserId && item.Favorite.FavoriteDestinationId == favoriteDestinationId).Favorite);

        public Task<IReadOnlyList<FavoriteDestinationDto>> FindPossibleDuplicatesAsync(string ownerUserId, string? name, string? address, Guid? excludingFavoriteDestinationId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FavoriteDestinationDto>>(_favorites
                .Where(item => item.Owner == ownerUserId
                    && item.Favorite.FavoriteDestinationId != excludingFavoriteDestinationId
                    && string.Equals(item.Favorite.Name.Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Favorite.Address.Trim(), address?.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Favorite).ToArray());

        public Task<FavoriteDestinationDto> CreateAsync(string ownerUserId, CreateFavoriteDestinationRequest request, PlaceAddressComponents? location, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            var favorite = ToDto(request, location, nowUtc);
            _favorites.Add((ownerUserId, favorite));
            return Task.FromResult(favorite);
        }

        public Task<FavoriteDestinationDto?> UpdateAsync(string ownerUserId, Guid favoriteDestinationId, UpdateFavoriteDestinationRequest request, PlaceAddressComponents? location, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            var index = _favorites.FindIndex(item => item.Owner == ownerUserId && item.Favorite.FavoriteDestinationId == favoriteDestinationId);
            if (index < 0) return Task.FromResult<FavoriteDestinationDto?>(null);
            var updated = _favorites[index].Favorite with
            {
                Name = request.Name!.Trim(),
                Address = request.Address!.Trim(),
                City = location?.City,
                Country = location?.Country,
                Latitude = location?.Latitude,
                Longitude = location?.Longitude,
                Notes = request.Notes,
                UpdatedAtUtc = nowUtc
            };
            _favorites[index] = (ownerUserId, updated);
            return Task.FromResult<FavoriteDestinationDto?>(updated);
        }

        public Task<IReadOnlyList<FavoriteDestinationDto>> CreateManyAsync(string ownerUserId, IReadOnlyList<(CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)> favorites, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<FavoriteDestinationDto> created = favorites
                .Select(item => ToDto(item.Request, item.Location, nowUtc))
                .ToArray();
            _favorites.AddRange(created.Select(favorite => (ownerUserId, favorite)));
            return Task.FromResult(created);
        }

        private static FavoriteDestinationDto ToDto(CreateFavoriteDestinationRequest request, PlaceAddressComponents? location, DateTimeOffset nowUtc)
            => new(Guid.NewGuid(), request.Name!.Trim(), request.Address!.Trim(), location?.City, location?.Country, location?.Latitude, location?.Longitude, request.Notes, nowUtc, nowUtc);

        public Task<bool> DeleteAsync(string ownerUserId, Guid favoriteDestinationId, CancellationToken cancellationToken = default)
        {
            var index = _favorites.FindIndex(item => item.Owner == ownerUserId && item.Favorite.FavoriteDestinationId == favoriteDestinationId);
            if (index < 0) return Task.FromResult(false);
            _favorites.RemoveAt(index);
            return Task.FromResult(true);
        }

        public Task<int> DeleteManyAsync(string ownerUserId, IReadOnlyCollection<Guid> favoriteDestinationIds, CancellationToken cancellationToken = default)
            => Task.FromResult(_favorites.RemoveAll(item => item.Owner == ownerUserId && favoriteDestinationIds.Contains(item.Favorite.FavoriteDestinationId)));
    }
}

internal static class FavoriteTestAuthExtensions
{
    public static void AddUser(this HttpClient client, string userId)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
    }
}