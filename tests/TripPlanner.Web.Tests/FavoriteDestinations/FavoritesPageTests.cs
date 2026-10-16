using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Web.Components.Shared;
using TripPlanner.Web.Features.FavoriteDestinations;
using FavoriteEntryPage = TripPlanner.Web.Components.Pages.FavoriteDestinations.FavoriteEntry;
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
        Assert.Equal("Favorites", cut.Find("header.entry-page-heading h1").TextContent);
        Assert.Single(cut.FindAll("h1"));
        Assert.Equal("-1", cut.Find("h1").GetAttribute("tabindex"));
        Assert.Equal("Places you want to remember for a future trip.", cut.Find(".entry-page-subtitle").TextContent);
        Assert.Equal("favorite", cut.FindComponent<EntryPageHeading>().Instance.Icon);
        Assert.NotNull(cut.Find(".entry-page-icon svg[aria-hidden='true']"));
        Assert.Equal("/favorites/new?returnUrl=%2Ffavorites", cut.Find("#favorite-add").GetAttribute("href"));
        Assert.Equal("/favorites/new?returnUrl=%2Ffavorites", cut.Find("#favorite-add-empty").GetAttribute("href"));
    }

    [Fact]
    public void CreateEntry_LoadsInsideCardWithoutAnErrorBanner()
    {
        var cut = Render<FavoriteEntryPage>();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".entry-form-card #favorite-form > .card-body")));
        Assert.Equal("Add a favorite", cut.Find(".entry-page-heading h1").TextContent);
        Assert.Equal("submit", cut.Find(".card-footer .entry-form-actions").Children[0].GetAttribute("type"));
        Assert.Equal(["Save", "Cancel"], cut.FindAll(".card-footer button").Select(button => button.TextContent.Trim()));
        Assert.Equal("Add a favorite", cut.Find("h1").TextContent);
        Assert.Equal("Keep a destination handy to use when planning your trips.", cut.Find(".entry-page-subtitle").TextContent);
        Assert.NotNull(cut.Find("header.entry-page-heading + .entry-form-card"));
        Assert.Empty(cut.FindAll(".entry-form-card > .card-header"));
        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.Empty(cut.FindAll(".validation-message"));
    }

    [Fact]
    public void EditEntry_EndsBreadcrumbAtFavoriteAndLoadsWithoutAnErrorBanner()
    {
        var favorite = Favorite("Museum Island", "Berlin", "Berlin", "Germany", null);
        Services.AddSingleton<IFavoriteDestinationApiClient>(new RecordingFavoriteDestinationApiClient([favorite]));
        var cut = Render<FavoriteEntryPage>(parameters => parameters.Add(page => page.FavoriteId, favorite.FavoriteDestinationId));

        cut.WaitForAssertion(() => Assert.Equal("Museum Island", cut.Find("#favorite-name").GetAttribute("value")));
        Assert.Equal(["Home", "Favorites", "Museum Island"], cut.FindAll(".breadcrumb-item").Select(item => item.TextContent.Trim()));
        Assert.Equal("Museum Island", cut.Find("[aria-current='page']").TextContent);
        Assert.Equal("Edit this favorite", cut.Find("h1").TextContent);
        Assert.NotNull(cut.Find(".entry-form-card #favorite-form > .card-body"));
        Assert.Equal(["Save", "Cancel", "Delete"], cut.FindAll(".card-footer button").Select(button => button.TextContent.Trim()));
        Assert.Equal(["Save this favorite destination", "Cancel without saving this favorite", "Confirm deletion of this favorite"], cut.FindAll(".card-footer button").Select(button => button.GetAttribute("title")));
        Assert.Contains("ms-auto", cut.Find("#favorite-delete").ClassList);
        Assert.All(cut.FindAll(".card-footer button"), button => Assert.NotNull(button.QuerySelector("svg")));
        Assert.Equal("Keep a destination handy to use when planning your trips.", cut.Find(".entry-page-subtitle").TextContent);
        Assert.Equal("favorite", cut.FindComponent<TripPlanner.Web.Components.Shared.EntryPageShell>().Instance.Icon);
        Assert.Empty(cut.FindAll("[role='alert']"));
        Assert.Empty(cut.FindAll(".validation-message"));
    }

    [Fact]
    public void CreateEntry_ShowsActualSaveErrorAndRetainsEnteredValues()
    {
        var client = new RecordingFavoriteDestinationApiClient { CreateErrorMessage = "The favorite could not be saved. Try again." };
        Services.AddSingleton<IFavoriteDestinationApiClient>(client);
        var cut = Render<FavoriteEntryPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#favorite-form")));
        Assert.Empty(cut.FindAll("[role='alert']"));

        cut.Find("#favorite-name").Change("Museum Island");
        cut.Find("#favorite-address").Change("Berlin");
        cut.Find("#favorite-form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(client.CreateErrorMessage, cut.Find("[role='alert']").TextContent));
        Assert.Equal("Museum Island", cut.Find("#favorite-name").GetAttribute("value"));
        Assert.Equal("Berlin", cut.Find("#favorite-address").GetAttribute("value"));
        Assert.False(cut.Find("button[type='submit']").HasAttribute("disabled"));
    }

    [Fact]
    public void CreateForm_IdentifiesMissingNameAndAddress()
    {
        var cut = Render<FavoriteEntryPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#favorite-form")));
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
        var edit = cut.Find($"[data-testid='favorite-edit-{favorite.FavoriteDestinationId}']");
        Assert.Equal($"/favorites/{favorite.FavoriteDestinationId:D}/edit?returnUrl=%2Ffavorites", edit.GetAttribute("href"));
        Assert.Empty(cut.FindAll("#favorite-form"));
    }

    [Fact]
    public void ImportTab_ShowsImporterInsteadOfList_AndCancelReturnsToList()
    {
        var cut = Render<FavoritesPage>();
        cut.WaitForAssertion(() => Assert.Contains("No favorite destinations yet", cut.Markup));
        Assert.Empty(cut.FindAll(".favorite-import"));
        Assert.Equal("/favorites", cut.Find("#favorite-tab-list").GetAttribute("href"));
        Assert.Equal("/favorites/import?returnUrl=%2Ffavorites", cut.Find("#favorite-tab-import").GetAttribute("href"));
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

        cut.WaitForAssertion(() => Assert.StartsWith("1", cut.Find("[data-testid='favorite-import-badge']").TextContent));
        Assert.Equal($"/favorites/import/{client.OpenImports[0].ImportId:D}?returnUrl=%2Ffavorites", cut.Find("#favorite-tab-import").GetAttribute("href"));
        Assert.Empty(cut.FindAll("[data-testid='favorite-import-ambiguity-1']"));
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
        Assert.Equal($"/favorites/import/{processing.ImportId:D}?returnUrl=%2Ffavorites", cut.Find("#favorite-tab-import").GetAttribute("href"));
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
        Assert.All(cut.FindAll(".favorites-table tbody a"), anchor => Assert.StartsWith("/favorites/", anchor.GetAttribute("href")));
        Assert.False(link.HasAttribute("href"));

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
        var cut = Render<FavoriteEntryPage>();
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#favorite-form")));
        cut.Find("#favorite-name").Change("Museum Island");
        cut.Find("#favorite-address").Change("Bodestraße 1, Berlin");
        cut.Find("#favorite-form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("possible duplicate", cut.Markup, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, client.CreateCallCount);
        cut.FindAll("button").Single(button => button.TextContent.Contains("Save separately", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, client.CreateCallCount));
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
    public string? CreateErrorMessage { get; init; }
    public CreateFavoriteDestinationRequest? LastCreateRequest { get; private set; }

    public Task<IReadOnlyList<FavoriteDestinationDto>> GetAsync(string? search = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FavoriteDestinationDto>>(_favorites);

    public Task<FavoriteDestinationMutationResult> CreateAsync(CreateFavoriteDestinationRequest request, CancellationToken ct = default)
    {
        CreateCallCount++;
        LastCreateRequest = request;
        if (CreateErrorMessage is not null)
        {
            return Task.FromResult(new FavoriteDestinationMutationResult(null, null, CreateErrorMessage));
        }
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