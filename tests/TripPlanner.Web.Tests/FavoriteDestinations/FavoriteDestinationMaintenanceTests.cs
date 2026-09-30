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

    [Fact]
    public void DeleteMessage_ShowsSuccessIconAndCanBeDismissed()
    {
        var favorite = Favorite("First place", "First address", null, null, null);
        var api = new MaintenanceFavoriteDestinationApiClient([favorite]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("First place", cut.Markup));

        cut.Find($"[data-testid='favorite-select-{favorite.FavoriteDestinationId}']").Change(true);
        cut.Find("#favorite-delete-selected").Click();
        cut.Find("#favorite-bulk-delete-confirm").Click();

        cut.WaitForAssertion(() => Assert.Contains("1 favorite removed", cut.Find("[data-testid='favorite-save-message']").TextContent));
        var alert = cut.Find("[data-testid='favorite-save-message']");
        Assert.Contains("alert-success", alert.ClassList);
        Assert.NotNull(alert.QuerySelector("svg"));
        cut.Find("#favorite-save-message-close").Click();
        Assert.Empty(cut.FindAll("[data-testid='favorite-save-message']"));
    }

    [Fact]
    public void DeleteMessage_DisappearsAfterItsDuration()
    {
        var favorite = Favorite("First place", "First address", null, null, null);
        var api = new MaintenanceFavoriteDestinationApiClient([favorite]);
        var cut = RenderPage(api, TimeSpan.FromMilliseconds(50));
        cut.WaitForAssertion(() => Assert.Contains("First place", cut.Markup));

        cut.Find($"[data-testid='favorite-select-{favorite.FavoriteDestinationId}']").Change(true);
        cut.Find("#favorite-delete-selected").Click();
        cut.Find("#favorite-bulk-delete-confirm").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='favorite-save-message']")), TimeSpan.FromSeconds(5));
        Assert.Empty(api.Favorites);
    }

    [Fact]
    public void FavoriteMap_KeepClosesWithoutChanges()
    {
        var favorite = Favorite("Siam Bistro", "4129 Merchant Plaza, Woodbridge, VA 22192", "Woodbridge", "United States", null);
        var api = new MaintenanceFavoriteDestinationApiClient([favorite]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("Siam Bistro", cut.Markup));

        cut.Find($"[data-testid='favorite-map-{favorite.FavoriteDestinationId}']").Click();

        var modal = cut.Find("[data-testid='favorite-place-map']");
        Assert.Contains("4129 Merchant Plaza, Woodbridge, VA 22192", modal.TextContent);
        Assert.Single(cut.FindAll(".tp-map-canvas"));
        Assert.Equal("https://www.bing.com/maps?cp=38.65123~-77.25321&lvl=17&sp=point.38.65123_-77.25321_Siam%20Bistro", cut.Find("#favorite-place-map-open").GetAttribute("href"));
        cut.Find("#favorite-map-keep").Click();

        Assert.Empty(cut.FindAll("[data-testid='favorite-place-map']"));
        Assert.Equal(0, api.DeleteCallCount);
        Assert.Single(api.Favorites);
    }

    [Fact]
    public void FavoriteMap_CloseButtonClosesWithoutChanges()
    {
        var favorite = Favorite("Siam Bistro", "4129 Merchant Plaza, Woodbridge, VA 22192", "Woodbridge", "United States", null);
        var api = new MaintenanceFavoriteDestinationApiClient([favorite]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("Siam Bistro", cut.Markup));

        cut.Find($"[data-testid='favorite-map-{favorite.FavoriteDestinationId}']").Click();
        cut.Find("#favorite-map-remove").Click();
        cut.Find("#favorite-place-map-close").Click();

        Assert.Empty(cut.FindAll("[data-testid='favorite-place-map']"));
        Assert.Equal(0, api.DeleteCallCount);
        Assert.Single(api.Favorites);

        cut.Find($"[data-testid='favorite-map-{favorite.FavoriteDestinationId}']").Click();
        Assert.Empty(cut.FindAll("[data-testid='favorite-map-remove-warning']"));
    }

    [Fact]
    public void FavoriteMap_RemoveRequiresConfirmation()
    {
        var favorite = Favorite("Wrong place", "Somewhere", null, null, null);
        var kept = Favorite("Kept place", "Elsewhere", null, null, null);
        var api = new MaintenanceFavoriteDestinationApiClient([favorite, kept]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("Wrong place", cut.Markup));

        cut.Find($"[data-testid='favorite-map-{favorite.FavoriteDestinationId}']").Click();
        cut.Find("#favorite-map-remove").Click();
        Assert.Contains("Remove Wrong place from your favorites?", cut.Find("[data-testid='favorite-map-remove-warning']").TextContent);
        Assert.Equal(0, api.DeleteCallCount);
        cut.Find("#favorite-map-remove-cancel").Click();
        Assert.Empty(cut.FindAll("[data-testid='favorite-map-remove-warning']"));

        cut.Find("#favorite-map-remove").Click();
        cut.Find("#favorite-map-remove-confirm").Click();

        cut.WaitForAssertion(() => Assert.Contains("Favorite destination removed", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='favorite-place-map']"));
        Assert.Equal(1, api.DeleteCallCount);
        Assert.Equal("Kept place", Assert.Single(api.Favorites).Name);
    }

    [Fact]
    public void FavoriteMap_WithoutCoordinates_ExplainsAndOffersOriginalLink()
    {
        const string googleUrl = "https://www.google.com/maps/place/Eiffel+Tower/data=!4m2!3m1!1s0x0:0x1";
        var favorite = new FavoriteDestinationDto(Guid.NewGuid(), "Eiffel Tower", googleUrl, null, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var api = new MaintenanceFavoriteDestinationApiClient([favorite]);
        var cut = RenderPage(api);
        cut.WaitForAssertion(() => Assert.Contains("Eiffel Tower", cut.Markup));

        cut.Find($"[data-testid='favorite-map-{favorite.FavoriteDestinationId}']").Click();

        Assert.NotNull(cut.Find("[data-testid='favorite-place-map-unplaced']"));
        Assert.Empty(cut.FindAll(".tp-map-canvas"));
        Assert.Equal(googleUrl, cut.Find("#favorite-place-map-open").GetAttribute("href"));
        Assert.NotNull(cut.Find("#favorite-map-remove"));
    }

    private IRenderedComponent<FavoritesPage> RenderPage(MaintenanceFavoriteDestinationApiClient api, TimeSpan? saveMessageDuration = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IFavoriteDestinationApiClient>(api);
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        return Render<FavoritesPage>(parameters => parameters
            .Add(page => page.SaveMessageDuration, saveMessageDuration ?? TimeSpan.FromSeconds(5)));
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

    public Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<FavoriteDestinationImportJobDto>> GetOpenImportsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FavoriteDestinationImportJobDto>>(Array.Empty<FavoriteDestinationImportJobDto>());

    public Task<FavoriteDestinationImportJobDto?> GetImportAsync(Guid importId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<FavoriteDestinationImportCompletion> CompleteImportAsync(Guid importId, CompleteFavoriteDestinationImportRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> DiscardImportAsync(Guid importId, CancellationToken ct = default)
        => throw new NotSupportedException();
}