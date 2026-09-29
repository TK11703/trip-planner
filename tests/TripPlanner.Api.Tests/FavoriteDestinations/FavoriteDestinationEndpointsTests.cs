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
    public async Task Import_GoogleCsv_ReplacesUrlWithResolvedAddress()
    {
        await using var factory = new FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-a");
        factory.Lookup.ResolutionsByAddress["Galeries Lafayette Haussmann"] = new PlaceAddressComponents(
            "Paris",
            "France",
            48.8738,
            2.332,
            "40 Boulevard Haussmann, 75009 Paris, France");
        const string csv = """
        Title,Note,URL,Tags,Comment
        Galeries Lafayette Haussmann,,https://www.google.com/maps/place/Galeries+Lafayette+Haussmann/data=!4m2!3m1!1s0x47e66e3703a1108b:0xe6773845cdab1593,,
        """;
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(csv), "file", "favorites.csv");

        var response = await client.PostAsync("/api/favorite-destinations/import", form);
        var queued = await response.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var completed = await FavoriteDestinationImportEndpointTests.WaitForImportAsync(client, queued!.Import!.ImportId);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(FavoriteDestinationImportStatus.Completed, completed.Status);
        var imported = Assert.Single(factory.Repository.All).Favorite;
        Assert.Equal("40 Boulevard Haussmann, 75009 Paris, France", imported.Address);
        Assert.Equal("Paris", imported.City);
        Assert.Equal("France", imported.Country);
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
        public FavoritesApiFactory() => Imports = new InMemoryFavoriteDestinationImportRepository(Repository);

        public InMemoryFavoriteDestinationRepository Repository { get; } = new();
        public InMemoryFavoriteDestinationImportRepository Imports { get; }
        public FakePlaceLookup Lookup { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFavoriteDestinationRepository>();
                services.AddSingleton<IFavoriteDestinationRepository>(Repository);
                services.RemoveAll<IFavoriteDestinationImportRepository>();
                services.AddSingleton<IFavoriteDestinationImportRepository>(Imports);
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
        public Dictionary<string, PlaceMatch[]> MatchesByQuery { get; } = new(StringComparer.OrdinalIgnoreCase);
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

        public async Task<IReadOnlyList<PlaceMatch>> FindPlacesAsync(string query, CancellationToken ct)
        {
            if (MatchesByQuery.TryGetValue(query, out var matches))
            {
                return matches;
            }

            var resolved = await ResolveAddressAsync(query, ct);
            return resolved is null ? Array.Empty<PlaceMatch>() : [new PlaceMatch(null, resolved)];
        }
    }

    internal sealed class InMemoryFavoriteDestinationImportRepository(InMemoryFavoriteDestinationRepository favorites) : IFavoriteDestinationImportRepository
    {
        private readonly Lock _gate = new();
        private readonly Dictionary<Guid, (FavoriteDestinationImportJob Job, List<FavoriteDestinationImportRowState> Rows, DateTimeOffset? LeaseExpiresAtUtc)> _imports = [];

        public Task<FavoriteDestinationImportJob> CreateAsync(string ownerUserId, string fileName, IReadOnlyList<FavoriteDestinationImportRowInput> rows, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            var job = new FavoriteDestinationImportJob(Guid.NewGuid(), ownerUserId, fileName, FavoriteDestinationImportStatus.Queued, rows.Count, 0, 0, null, 0, nowUtc, nowUtc);
            var states = rows.Select(row => new FavoriteDestinationImportRowState(
                row.RowNumber, row.Name, row.SubmittedAddress, row.Notes, FavoriteDestinationImportRowStatus.Pending, false, null, null, [], false)).ToList();
            lock (_gate)
            {
                _imports[job.ImportId] = (job, states, null);
            }
            return Task.FromResult(job);
        }

        public Task<FavoriteDestinationImportJob?> GetAsync(string ownerUserId, Guid importId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_imports.TryGetValue(importId, out var entry) && entry.Job.OwnerUserId == ownerUserId
                    ? WithProgress(entry.Job, entry.Rows)
                    : null);
            }
        }

        public Task<IReadOnlyList<FavoriteDestinationImportJob>> GetOpenAsync(string ownerUserId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                IReadOnlyList<FavoriteDestinationImportJob> open = _imports.Values
                    .Where(entry => entry.Job.OwnerUserId == ownerUserId && entry.Job.Status != FavoriteDestinationImportStatus.Completed)
                    .OrderByDescending(entry => entry.Job.CreatedAtUtc)
                    .Select(entry => WithProgress(entry.Job, entry.Rows))
                    .ToArray();
                return Task.FromResult(open);
            }
        }

        public Task<IReadOnlyList<FavoriteDestinationImportRowState>> GetRowsAsync(string ownerUserId, Guid importId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                IReadOnlyList<FavoriteDestinationImportRowState> rows = _imports.TryGetValue(importId, out var entry) && entry.Job.OwnerUserId == ownerUserId
                    ? entry.Rows.ToArray()
                    : [];
                return Task.FromResult(rows);
            }
        }

        public Task<FavoriteDestinationImportJob?> ClaimNextAsync(DateTimeOffset nowUtc, DateTimeOffset leaseExpiresAtUtc, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                var next = _imports.Values
                    .Where(entry => entry.Job.Status == FavoriteDestinationImportStatus.Queued
                        || (entry.Job.Status == FavoriteDestinationImportStatus.Processing && entry.LeaseExpiresAtUtc < nowUtc))
                    .OrderBy(entry => entry.Job.CreatedAtUtc)
                    .Select(entry => (FavoriteDestinationImportJob?)entry.Job)
                    .FirstOrDefault();
                if (next is null)
                {
                    return Task.FromResult<FavoriteDestinationImportJob?>(null);
                }

                var claimed = next with { Status = FavoriteDestinationImportStatus.Processing, AttemptCount = next.AttemptCount + 1, UpdatedAtUtc = nowUtc };
                _imports[claimed.ImportId] = (claimed, _imports[claimed.ImportId].Rows, leaseExpiresAtUtc);
                return Task.FromResult<FavoriteDestinationImportJob?>(claimed);
            }
        }

        public Task<bool> SaveRowResolutionAsync(Guid importId, FavoriteDestinationImportRowResolution resolution, DateTimeOffset nowUtc, DateTimeOffset leaseExpiresAtUtc, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!_imports.TryGetValue(importId, out var entry) || entry.Job.Status != FavoriteDestinationImportStatus.Processing)
                {
                    return Task.FromResult(false);
                }

                var index = entry.Rows.FindIndex(row => row.RowNumber == resolution.RowNumber);
                entry.Rows[index] = entry.Rows[index] with
                {
                    Status = resolution.Status,
                    UsesResolvedAddress = resolution.UsesResolvedAddress,
                    Address = resolution.Address,
                    Location = resolution.Location,
                    Candidates = resolution.Candidates
                };
                _imports[importId] = (entry.Job with { UpdatedAtUtc = nowUtc }, entry.Rows, leaseExpiresAtUtc);
                return Task.FromResult(true);
            }
        }

        public Task<bool> MarkNeedsReviewAsync(Guid importId, IReadOnlyCollection<int> duplicateRowNumbers, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!_imports.TryGetValue(importId, out var entry) || entry.Job.Status != FavoriteDestinationImportStatus.Processing)
                {
                    return Task.FromResult(false);
                }

                var rows = entry.Rows.Select(row => row with { IsPossibleDuplicate = duplicateRowNumbers.Contains(row.RowNumber) }).ToList();
                _imports[importId] = (entry.Job with { Status = FavoriteDestinationImportStatus.NeedsReview, UpdatedAtUtc = nowUtc }, rows, null);
                return Task.FromResult(true);
            }
        }

        public Task MarkFailedAsync(Guid importId, string errorMessage, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_imports.TryGetValue(importId, out var entry))
                {
                    _imports[importId] = (entry.Job with { Status = FavoriteDestinationImportStatus.Failed, ErrorMessage = errorMessage, UpdatedAtUtc = nowUtc }, entry.Rows, null);
                }
            }
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<FavoriteDestinationDto>?> CompleteAsync(string ownerUserId, Guid importId, FavoriteDestinationImportStatus expectedStatus, IReadOnlyList<(CreateFavoriteDestinationRequest Request, PlaceAddressComponents? Location)> favoritesToCreate, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (!_imports.TryGetValue(importId, out var entry) || entry.Job.OwnerUserId != ownerUserId || entry.Job.Status != expectedStatus)
                {
                    return null;
                }

                _imports[importId] = (entry.Job with { Status = FavoriteDestinationImportStatus.Completed, ImportedCount = favoritesToCreate.Count, UpdatedAtUtc = nowUtc }, [], null);
            }
            return await favorites.CreateManyAsync(ownerUserId, favoritesToCreate, nowUtc, cancellationToken);
        }

        public Task<bool> DeleteAsync(string ownerUserId, Guid importId, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_imports.TryGetValue(importId, out var entry) && entry.Job.OwnerUserId == ownerUserId && _imports.Remove(importId));
            }
        }

        private static FavoriteDestinationImportJob WithProgress(FavoriteDestinationImportJob job, IReadOnlyList<FavoriteDestinationImportRowState> rows)
            => job with
            {
                ProcessedRows = job.Status == FavoriteDestinationImportStatus.Completed
                    ? job.TotalRows
                    : rows.Count(row => row.Status != FavoriteDestinationImportRowStatus.Pending)
            };
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