using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Web.Features.FavoriteDestinations;
using TripPlanner.Web.Tests.Auth;
using FavoritesPage = TripPlanner.Web.Components.Pages.Favorites;

namespace TripPlanner.Web.Tests.FavoriteDestinations;

public sealed class FavoriteDestinationMaintenanceTests : BunitContext
{
    [Fact]
    public void Search_FiltersNameAddressCityCountryAndNotes_AndSupportsNoResults()
    {
        var favorite = Favorite("NameSearchToken", "AddressSearchToken", "CitySearchToken", "CountrySearchToken", "NotesSearchToken");
        var unrelated = Favorite("Unrelated place", "Other address", "Other city", "Other country", "Other notes");
        var api = new MaintenanceFavoriteDestinationApiClient([favorite, unrelated]);
        var cut = RenderPage(api);

        cut.WaitForAssertion(() => Assert.Contains("NameSearchToken", cut.Markup));
        foreach (var term in new[] { "namesearchtoken", "ADDRESSSEARCHTOKEN", "citysearchtoken", "CountrySearchToken", "notessearchtoken" })
        {
            cut.Find("#favorite-search").Input(term);
            cut.WaitForAssertion(() =>
            {
                Assert.Equal(term, api.LastSearch);
                Assert.Contains("NameSearchToken", cut.Markup);
                Assert.DoesNotContain("Unrelated place", cut.Markup);
            });
        }

        cut.Find("#favorite-search").Input("no such destination");
        cut.WaitForAssertion(() => Assert.Contains("No favorites match your search", cut.Markup));
        Assert.NotNull(cut.Find("#favorite-search"));
    }

    [Fact]
    public void EditPersistsFullResearchContext_AndDeleteRequiresConfirmation()
    {
        var favorite = Favorite("Old name", "Old address", "Old city", "Old country", new string('n', 800));
        var api = new MaintenanceFavoriteDestinationApiClient([favorite]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("Old name", cut.Markup));
        Assert.DoesNotContain(new string('n', 800), cut.Markup);

        cut.Find($"[data-testid='favorite-edit-{favorite.FavoriteDestinationId}']").Click();
        Assert.Equal(new string('n', 800), cut.Find("#favorite-notes").GetAttribute("value"));
        Assert.Empty(cut.FindAll("#favorite-city"));
        Assert.Empty(cut.FindAll("#favorite-country"));
        Assert.Empty(cut.FindAll("#favorite-source"));
        Assert.Contains("Old city · Old country · 38.65123, -77.25321", cut.Find("[data-testid='favorite-calculated-location']").TextContent);
        cut.Find("#favorite-name").Change("Updated name");
        cut.Find("#favorite-notes").Change("Updated notes");
        cut.Find("#favorite-form").Submit();
        cut.WaitForAssertion(() => Assert.Contains("Updated name", cut.Markup));
        Assert.Equal("Updated notes", api.Favorites.Single().Notes);
        Assert.Empty(cut.FindAll(".modal"));

        cut.Find($"[data-testid='favorite-edit-{favorite.FavoriteDestinationId}']").Click();
        cut.Find("#favorite-delete").Click();
        Assert.Contains("favorite-delete-confirm", cut.Markup);
        Assert.Equal(0, api.DeleteCallCount);
        cut.Find("#favorite-delete-confirm-action").Click();

        cut.WaitForAssertion(() => Assert.Contains("No favorite destinations yet", cut.Markup));
        Assert.Empty(cut.FindAll(".modal"));
        Assert.Equal(1, api.DeleteCallCount);
    }

    [Fact]
    public void SelectedFavorites_AreDeletedTogetherAfterConfirmation()
    {
        var first = Favorite("First place", "First address", null, null, null);
        var second = Favorite("Second place", "Second address", null, null, null);
        var kept = Favorite("Kept place", "Kept address", null, null, null);
        var api = new MaintenanceFavoriteDestinationApiClient([first, second, kept]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("Kept place", cut.Markup));
        Assert.True(cut.Find("#favorite-delete-selected").HasAttribute("disabled"));

        cut.Find($"[data-testid='favorite-select-{first.FavoriteDestinationId}']").Change(true);
        cut.Find($"[data-testid='favorite-select-{second.FavoriteDestinationId}']").Change(true);
        Assert.Equal("Delete (2)", cut.Find("#favorite-delete-selected").TextContent.Trim());
        cut.Find("#favorite-delete-selected").Click();

        Assert.Contains("Remove 2 selected favorites?", cut.Find(".modal-body").TextContent);
        Assert.Null(api.LastBulkDeleteIds);
        cut.Find("#favorite-bulk-delete-confirm").Click();

        cut.WaitForAssertion(() => Assert.Contains("2 favorites removed", cut.Markup));
        Assert.Equal(new[] { first.FavoriteDestinationId, second.FavoriteDestinationId }.Order(), api.LastBulkDeleteIds!.Order());
        Assert.Equal("Kept place", Assert.Single(api.Favorites).Name);
        Assert.Empty(cut.FindAll(".modal"));
        Assert.True(cut.Find("#favorite-delete-selected").HasAttribute("disabled"));
    }

    [Fact]
    public void SelectAll_SelectsOnlyListedFavorites()
    {
        var match = Favorite("Museum match", "Berlin", null, null, null);
        var hidden = Favorite("Other place", "Paris", null, null, null);
        var api = new MaintenanceFavoriteDestinationApiClient([match, hidden]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("Other place", cut.Markup));

        cut.Find($"[data-testid='favorite-select-{hidden.FavoriteDestinationId}']").Change(true);
        cut.Find("#favorite-search").Input("museum");
        cut.WaitForAssertion(() => Assert.DoesNotContain("Other place", cut.Markup));
        cut.Find("#favorite-select-all").Change(true);
        cut.Find("#favorite-delete-selected").Click();
        cut.Find("#favorite-bulk-delete-confirm").Click();

        cut.WaitForAssertion(() => Assert.Contains("1 favorite removed", cut.Markup));
        Assert.Equal([match.FavoriteDestinationId], api.LastBulkDeleteIds);
        Assert.Contains(api.Favorites, favorite => favorite.FavoriteDestinationId == hidden.FavoriteDestinationId);
    }

    private IRenderedComponent<FavoritesPage> RenderPage(MaintenanceFavoriteDestinationApiClient api)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IFavoriteDestinationApiClient>(api);
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        return Render<FavoritesPage>();
    }

    private static FavoriteDestinationDto Favorite(
        string name,
        string address,
        string? city,
        string? country,
        string? notes)
        => new(Guid.NewGuid(), name, address, city, country, 38.65123, -77.25321, notes, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}

internal sealed class MaintenanceFavoriteDestinationApiClient : IFavoriteDestinationApiClient
{
    public List<FavoriteDestinationDto> Favorites { get; }
    public string? LastSearch { get; private set; }
    public int DeleteCallCount { get; private set; }

    public MaintenanceFavoriteDestinationApiClient(IEnumerable<FavoriteDestinationDto> favorites)
        => Favorites = favorites.ToList();

    public Task<IReadOnlyList<FavoriteDestinationDto>> GetAsync(string? search = null, CancellationToken ct = default)
    {
        LastSearch = search;
        IEnumerable<FavoriteDestinationDto> results = Favorites;
        if (!string.IsNullOrWhiteSpace(search))
        {
            results = results.Where(favorite => new[] { favorite.Name, favorite.Address, favorite.City, favorite.Country, favorite.Notes }
                .Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true));
        }
        return Task.FromResult<IReadOnlyList<FavoriteDestinationDto>>(results.ToArray());
    }

    public Task<FavoriteDestinationMutationResult> CreateAsync(CreateFavoriteDestinationRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<FavoriteDestinationMutationResult> UpdateAsync(Guid id, UpdateFavoriteDestinationRequest request, CancellationToken ct = default)
    {
        var index = Favorites.FindIndex(favorite => favorite.FavoriteDestinationId == id);
        if (index < 0)
        {
            return Task.FromResult(new FavoriteDestinationMutationResult(null, null, "Favorite not found."));
        }

        var updated = Favorites[index] with
        {
            Name = request.Name!,
            Address = request.Address!,
            Notes = request.Notes,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        Favorites[index] = updated;
        return Task.FromResult(new FavoriteDestinationMutationResult(updated, null, null));
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        DeleteCallCount++;
        return Task.FromResult(Favorites.RemoveAll(favorite => favorite.FavoriteDestinationId == id) > 0);
    }

    public IReadOnlyCollection<Guid>? LastBulkDeleteIds { get; private set; }

    public Task<int?> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        LastBulkDeleteIds = ids;
        return Task.FromResult<int?>(Favorites.RemoveAll(favorite => ids.Contains(favorite.FavoriteDestinationId)));
    }

    public Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, bool confirmPossibleDuplicates = false, CancellationToken ct = default)
        => throw new NotSupportedException();
}