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
        var cells = cut.FindAll(".favorites-table tbody tr > *").Skip(1).Select(cell => cell.TextContent.Trim()).ToArray();
        Assert.Equal(["Museum Island", "Bodestraße 1, Berlin", "Berlin", "Germany"], cells);
        Assert.Empty(cut.FindAll(".modal"));

        cut.Find($"[data-testid='favorite-edit-{favorite.FavoriteDestinationId}']").Click();

        Assert.Equal("Edit favorite", cut.Find(".modal .modal-title").TextContent);
        Assert.Equal("Allow a full afternoon\n\nSource: Travel guide", cut.Find(".modal #favorite-notes").GetAttribute("value"));
        cut.Find("#favorite-cancel").Click();
        Assert.Empty(cut.FindAll(".modal"));
    }

    [Fact]
    public void ImportButton_ShowsImportCard_AndCancelHidesIt()
    {
        var cut = Render<FavoritesPage>();
        cut.WaitForAssertion(() => Assert.Contains("No favorite destinations yet", cut.Markup));
        Assert.Empty(cut.FindAll(".favorite-import"));

        cut.Find("#favorite-import-toggle").Click();

        Assert.Contains("card", cut.Find(".favorite-import").ClassList);
        Assert.NotNull(cut.Find("#favorite-import-submit"));
        cut.Find("#favorite-import-cancel").Click();
        Assert.Empty(cut.FindAll(".favorite-import"));
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

    public Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, bool confirmPossibleDuplicates = false, CancellationToken ct = default)
        => throw new NotSupportedException();
}