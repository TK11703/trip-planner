using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Web.Features.FavoriteDestinations;
using TripPlanner.Web.Tests.Auth;
using FavoritesPage = TripPlanner.Web.Components.Pages.Favorites;

namespace TripPlanner.Web.Tests.FavoriteDestinations;

public sealed class FavoritesPageTests : BunitContext
{
    public FavoritesPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IFavoriteDestinationApiClient>(new RecordingFavoriteDestinationApiClient());
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
    }

    [Fact]
    public void EmptyList_ShowsActionToAddFirstFavorite()
    {
        var cut = Render<FavoritesPage>();

        cut.WaitForAssertion(() => Assert.Contains("No favorite destinations yet", cut.Markup));
        cut.Find("#favorite-add").Click();

        Assert.NotNull(cut.Find("#favorite-form"));
    }

    [Fact]
    public void CreateForm_IdentifiesMissingNameAndAddress()
    {
        var cut = Render<FavoritesPage>();
        cut.WaitForAssertion(() => Assert.Contains("No favorite destinations yet", cut.Markup));
        cut.Find("#favorite-add").Click();
        cut.Find("#favorite-form").Submit();

        Assert.Contains("Name is required.", cut.Markup);
        Assert.Contains("Address is required.", cut.Markup);
        Assert.Equal(0, ((RecordingFavoriteDestinationApiClient)Services.GetRequiredService<IFavoriteDestinationApiClient>()).CreateCallCount);
    }

    [Fact]
    public void SavedFavorites_RenderAsTable_AndNameOpensEditModalWithNotes()
    {
        var favorite = Favorite("Museum Island", "Bodestraße 1, Berlin", "Berlin", "Germany", "Allow a full afternoon\n\nSource: Travel guide");
        Services.AddSingleton<IFavoriteDestinationApiClient>(new RecordingFavoriteDestinationApiClient([favorite]));

        var cut = Render<FavoritesPage>();

        cut.WaitForAssertion(() => Assert.Contains("Museum Island", cut.Markup));
        Assert.Equal(["", "Name", "Address", "City", "Country"], cut.FindAll(".favorites-table thead th").Select(th => th.TextContent.Trim()));
        Assert.NotNull(cut.Find("#favorite-select-all"));
        var cells = cut.FindAll(".favorites-table tbody tr > *").Skip(1).Select(cell => cell.TextContent.Trim()).Where(text => text.Length > 0).ToArray();
        Assert.Equal(["Museum Island", "Bodestraße 1, Berlin", "Berlin", "Germany"], cells);
        Assert.Empty(cut.FindAll(".modal"));

        cut.Find($"[data-testid='favorite-edit-{favorite.FavoriteDestinationId}']").Click();

        Assert.Equal("Edit favorite", cut.Find(".modal .modal-title").TextContent);
        Assert.Equal("Allow a full afternoon\n\nSource: Travel guide", cut.Find(".modal #favorite-notes").GetAttribute("value"));
        cut.Find("#favorite-cancel").Click();
        Assert.Empty(cut.FindAll(".modal"));
    }

    [Fact]
    public void ImportTab_ShowsImporterInsteadOfList_AndCancelReturnsToList()
    {
        var cut = Render<FavoritesPage>();
        cut.WaitForAssertion(() => Assert.Contains("No favorite destinations yet", cut.Markup));
        Assert.Empty(cut.FindAll(".favorite-import"));
        Assert.Equal("true", cut.Find("#favorite-tab-list").GetAttribute("aria-selected"));

        cut.Find("#favorite-tab-import").Click();

        Assert.Equal("true", cut.Find("#favorite-tab-import").GetAttribute("aria-selected"));
        Assert.NotNull(cut.Find("#favorite-import-submit"));
        Assert.Empty(cut.FindAll("#favorite-search"));
        cut.Find("#favorite-import-cancel").Click();
        Assert.Empty(cut.FindAll(".favorite-import"));
        Assert.NotNull(cut.Find("#favorite-search"));
    }

    [Fact]
    public void UnfinishedImport_OpensImportTabWithReview()
    {
        var client = new RecordingFavoriteDestinationApiClient();
        client.OpenImports.Add(new FavoriteDestinationImportJobDto(
            Guid.NewGuid(), "saved.csv", FavoriteDestinationImportStatus.NeedsReview, 1, 1, 0, null,
            [new FavoriteDestinationImportAmbiguity(1, "Starbucks", "https://maps.app.goo.gl/abc",
            [
                new FavoriteDestinationPlaceCandidate("Starbucks", "1912 Pike Pl, Seattle", "Seattle", "United States", 47.61, -122.34),
                new FavoriteDestinationPlaceCandidate("Starbucks", "1124 Pike St, Seattle", "Seattle", "United States", 47.6, -122.33)
            ])],
            [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        Services.AddSingleton<IFavoriteDestinationApiClient>(client);

        var cut = Render<FavoritesPage>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='favorite-import-ambiguity-1']")));
        Assert.Equal("true", cut.Find("#favorite-tab-import").GetAttribute("aria-selected"));
        Assert.StartsWith("1", cut.Find("[data-testid='favorite-import-badge']").TextContent);
    }

    [Fact]
    public void ProcessingImport_ShowsProgressOnTab_AndCompletionRefreshesList()
    {
        var imported = Favorite("Imported place", "Paris", "Paris", "France", null);
        var client = new RecordingFavoriteDestinationApiClient([imported]);
        var processing = new FavoriteDestinationImportJobDto(
            Guid.NewGuid(), "saved.csv", FavoriteDestinationImportStatus.Processing, 2, 1, 0, null, [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        client.OpenImports.Add(processing);
        Services.AddSingleton<IFavoriteDestinationApiClient>(client);
        var cut = Render<FavoritesPage>(parameters => parameters.Add(page => page.ImportPollInterval, TimeSpan.FromMilliseconds(20)));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='favorite-import-processing']")));
        cut.Find("#favorite-tab-list").Click();
        Assert.NotNull(cut.Find("[data-testid='favorite-import-processing']"));
        client.OpenImports[0] = processing with { Status = FavoriteDestinationImportStatus.Completed, ImportedCount = 2 };

        cut.WaitForAssertion(() => Assert.Contains("2 destinations imported", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='favorite-import-processing']"));
        Assert.Contains("Imported place", cut.Markup);
    }

    [Fact]
    public void KeptImportUrl_RendersAsCompactLinkThatOpensMapDialog()
    {
        const string googleUrl = "https://www.google.com/maps/place/Eiffel+Tower/data=!4m2!3m1!1s0x47e66e2964e34e2d:0x8ddca9ee380ef7e0";
        var unmatched = Favorite("Eiffel Tower", googleUrl, null, null, null);
        var matched = Favorite("Louvre", "Rue de Rivoli, Paris", "Paris", "France", null);
        var unsafeLink = Favorite("Odd import", "javascript:alert(1)", null, null, null);
        Services.AddSingleton<IFavoriteDestinationApiClient>(new RecordingFavoriteDestinationApiClient([unmatched, matched, unsafeLink]));

        var cut = Render<FavoritesPage>();

        cut.WaitForAssertion(() => Assert.Contains("Louvre", cut.Markup));
        var link = cut.Find($"[data-testid='favorite-map-{unmatched.FavoriteDestinationId}']");
        Assert.Equal("Unmatched Destination - Click here to view", link.TextContent.Trim());
        Assert.Contains("Eiffel Tower", link.GetAttribute("title"));
        Assert.DoesNotContain(googleUrl, cut.Find(".favorites-table tbody").TextContent);
        Assert.Equal("Rue de Rivoli, Paris", cut.Find($"[data-testid='favorite-map-{matched.FavoriteDestinationId}']").TextContent.Trim());
        Assert.Equal("javascript:alert(1)", cut.Find($"[data-testid='favorite-map-{unsafeLink.FavoriteDestinationId}']").TextContent.Trim());
        Assert.Empty(cut.FindAll(".favorites-table tbody a"));

        link.Click();

        Assert.Equal(googleUrl, cut.Find("#favorite-place-map-open").GetAttribute("href"));
        Assert.Equal("_blank", cut.Find("#favorite-place-map-open").GetAttribute("target"));
    }

    [Fact]
    public void DuplicateWarning_RequiresExplicitConfirmationBeforeCreate()
    {
        var existing = Favorite("Museum Island", "Bodestraße 1, Berlin", "Berlin", "Germany", null);
        var client = new RecordingFavoriteDestinationApiClient(duplicates: [existing]);
        Services.AddSingleton<IFavoriteDestinationApiClient>(client);
        var cut = Render<FavoritesPage>();
        cut.WaitForAssertion(() => Assert.Contains("No favorite destinations yet", cut.Markup));
        cut.Find("#favorite-add").Click();
        cut.Find("#favorite-name").Change("Museum Island");
        cut.Find("#favorite-address").Change("Bodestraße 1, Berlin");
        cut.Find("#favorite-form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("possible duplicate", cut.Markup, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, client.CreateCallCount);
        cut.Find("#favorite-confirm-duplicate").Click();

        cut.WaitForAssertion(() => Assert.Contains("Favorite destination saved", cut.Markup));
        Assert.Equal(2, client.CreateCallCount);
        Assert.True(client.LastCreateRequest!.ConfirmPossibleDuplicate);
    }

    private static FavoriteDestinationDto Favorite(
        string name,
        string address,
        string? city,
        string? country,
        string? notes)
        => new(Guid.NewGuid(), name, address, city, country, null, null, notes, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}

internal sealed class RecordingFavoriteDestinationApiClient : IFavoriteDestinationApiClient
{
    private readonly List<FavoriteDestinationDto> _favorites;
    private readonly IReadOnlyList<FavoriteDestinationDto> _duplicates;

    public RecordingFavoriteDestinationApiClient(
        IEnumerable<FavoriteDestinationDto>? favorites = null,
        IReadOnlyList<FavoriteDestinationDto>? duplicates = null)
    {
        _favorites = favorites?.ToList() ?? [];
        _duplicates = duplicates ?? [];
    }

    public int CreateCallCount { get; private set; }
    public CreateFavoriteDestinationRequest? LastCreateRequest { get; private set; }

    public Task<IReadOnlyList<FavoriteDestinationDto>> GetAsync(string? search = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FavoriteDestinationDto>>(_favorites);

    public Task<FavoriteDestinationMutationResult> CreateAsync(CreateFavoriteDestinationRequest request, CancellationToken ct = default)
    {
        CreateCallCount++;
        LastCreateRequest = request;
        if (_duplicates.Count > 0 && !request.ConfirmPossibleDuplicate)
        {
            return Task.FromResult(new FavoriteDestinationMutationResult(null, new FavoriteDestinationDuplicateWarning(_duplicates), null));
        }

        var favorite = new FavoriteDestinationDto(
            Guid.NewGuid(), request.Name!, request.Address!, null, null, null, null, request.Notes,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _favorites.Add(favorite);
        return Task.FromResult(new FavoriteDestinationMutationResult(favorite, null, null));
    }

    public Task<FavoriteDestinationMutationResult> UpdateAsync(Guid id, UpdateFavoriteDestinationRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<int?> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, CancellationToken ct = default)
        => throw new NotSupportedException();

    public List<FavoriteDestinationImportJobDto> OpenImports { get; } = [];

    public Task<IReadOnlyList<FavoriteDestinationImportJobDto>> GetOpenImportsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FavoriteDestinationImportJobDto>>(OpenImports);

    public Task<FavoriteDestinationImportJobDto?> GetImportAsync(Guid importId, CancellationToken ct = default)
        => Task.FromResult(OpenImports.FirstOrDefault(import => import.ImportId == importId));

    public Task<FavoriteDestinationImportCompletion> CompleteImportAsync(Guid importId, CompleteFavoriteDestinationImportRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> DiscardImportAsync(Guid importId, CancellationToken ct = default)
        => throw new NotSupportedException();
}