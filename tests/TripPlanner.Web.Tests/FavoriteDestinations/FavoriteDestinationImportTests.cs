using Bunit;
using System.Text;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Web.Components.Favorites;
using TripPlanner.Web.Features.FavoriteDestinations;
using TripPlanner.Web.Tests.Auth;

namespace TripPlanner.Web.Tests.FavoriteDestinations;

public sealed class FavoriteDestinationImportTests : BunitContext
{
    [Fact]
    public async Task Import_ShowsRowErrorsAndDoesNotReportSuccess()
    {
        var api = new ImportFavoriteDestinationApiClient(new FavoriteDestinationImportResponse(
            null,
            [new FavoriteDestinationImportIssue(3, "Address is required.")]));
        var cut = RenderImport(api);
        await SelectFileAsync(cut, "favorites.csv", "name,address,notes\r\nMuseum,,");
        cut.Find("#favorite-import-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Row 3: Address is required.", cut.Markup));
        Assert.DoesNotContain("destinations imported", cut.Markup);
        Assert.Equal(1, api.ImportCallCount);
    }

    [Fact]
    public async Task Import_ShowsBackgroundProgress_ThenReportsImportedCount()
    {
        var api = new ImportFavoriteDestinationApiClient(Queued(Job(FavoriteDestinationImportStatus.Queued)));
        api.PollResponses.Enqueue(Job(FavoriteDestinationImportStatus.Processing, processed: 1));
        api.PollResponses.Enqueue(Job(FavoriteDestinationImportStatus.Completed, processed: 2, imported: 2));
        var imported = 0;
        var cut = RenderImport(api, onImported: () => imported++);
        await SelectFileAsync(cut, "favorites.csv", "name,address,notes\r\nMuseum,Berlin,\r\nGarden,Paris,");
        cut.Find("#favorite-import-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("2 destinations imported", cut.Markup));
        Assert.Equal(1, imported);
        Assert.Empty(cut.FindAll("[data-testid='favorite-import-progress']"));
    }

    [Fact]
    public async Task AmbiguousPlace_ShowsNamesAddressesAndMapLinks_AndRequiresAChoice()
    {
        var review = Job(FavoriteDestinationImportStatus.NeedsReview, processed: 2) with
        {
            Ambiguities =
            [
                new FavoriteDestinationImportAmbiguity(2, "Blue Bottle Coffee", "https://maps.app.goo.gl/abc",
                [
                    new FavoriteDestinationPlaceCandidate("Blue Bottle Coffee", "300 Webster St, Oakland, CA 94607", "Oakland", "United States", 37.8, -122.27),
                    new FavoriteDestinationPlaceCandidate("Blue Bottle Coffee", "66 Mint St, San Francisco, CA 94103", "San Francisco", "United States", 37.78, -122.41)
                ])
            ]
        };
        var api = new ImportFavoriteDestinationApiClient(Queued(Job(FavoriteDestinationImportStatus.Queued)))
        {
            Completion = new FavoriteDestinationImportCompletion(Job(FavoriteDestinationImportStatus.Completed, processed: 2, imported: 2), null)
        };
        api.PollResponses.Enqueue(review);
        var cut = RenderImport(api);
        await SelectFileAsync(cut, "saved.csv", "Title,Note,URL,Tags,Comment");
        cut.Find("#favorite-import-submit").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid='favorite-import-ambiguity-2']")));
        var options = cut.Find("[data-testid='favorite-import-ambiguity-2']");
        Assert.Contains("66 Mint St, San Francisco, CA 94103", options.TextContent);
        Assert.Contains("300 Webster St, Oakland, CA 94607", options.TextContent);
        Assert.Empty(options.QuerySelectorAll("a"));
        Assert.True(cut.Find("#favorite-import-finish").HasAttribute("disabled"));

        cut.Find("[data-testid='favorite-import-map-2-1']").Click();
        var mapModal = cut.Find("[data-testid='favorite-place-map']");
        Assert.Contains("66 Mint St, San Francisco, CA 94103", mapModal.TextContent);
        Assert.Single(cut.FindAll(".tp-map-canvas"));
        var openExternally = cut.Find("#favorite-place-map-open");
        Assert.Equal("https://www.bing.com/maps?cp=37.78~-122.41&lvl=17&sp=point.37.78_-122.41_Blue%20Bottle%20Coffee", openExternally.GetAttribute("href"));
        Assert.Equal("_blank", openExternally.GetAttribute("target"));
        Assert.Contains("noopener", openExternally.GetAttribute("rel"));
        cut.Find("#favorite-place-map-close").Click();
        Assert.Empty(cut.FindAll("[data-testid='favorite-place-map']"));

        cut.Find("[data-testid='favorite-import-map-2-1']").Click();
        Assert.Equal("Select location", cut.Find("#favorite-place-map-select").TextContent.Trim());
        cut.Find("#favorite-place-map-select").Click();
        Assert.Empty(cut.FindAll("[data-testid='favorite-place-map']"));
        Assert.True(cut.Find("#favorite-import-2-1").HasAttribute("checked"));
        cut.Find("[data-testid='favorite-import-map-2-1']").Click();
        Assert.True(cut.Find("#favorite-place-map-select").HasAttribute("disabled"));
        cut.Find("#favorite-place-map-close-x").Click();

        cut.Find("#favorite-import-finish").Click();

        cut.WaitForAssertion(() => Assert.Contains("2 destinations imported", cut.Markup));
        var selection = Assert.Single(api.LastCompletion!.Selections!);
        Assert.Equal((2, 1), (selection.RowNumber, selection.CandidateIndex));
    }

    [Fact]
    public void ResumedReview_WithDuplicates_RequiresConfirmationBeforeFinishing()
    {
        var review = Job(FavoriteDestinationImportStatus.NeedsReview, processed: 2) with
        {
            PossibleDuplicates = [new FavoriteDestinationImportDuplicate(2, "Museum", "Berlin")]
        };
        var api = new ImportFavoriteDestinationApiClient
        {
            Completion = new FavoriteDestinationImportCompletion(Job(FavoriteDestinationImportStatus.Completed, processed: 2, imported: 2), null)
        };
        api.OpenImports.Add(review);
        var cut = RenderImport(api);

        cut.WaitForAssertion(() => Assert.Contains("Row 2: Museum, Berlin", cut.Markup));
        Assert.True(cut.Find("#favorite-import-finish").HasAttribute("disabled"));
        cut.Find("#favorite-import-confirm-duplicates").Change(true);
        cut.Find("#favorite-import-finish").Click();

        cut.WaitForAssertion(() => Assert.Contains("2 destinations imported", cut.Markup));
        Assert.True(api.LastCompletion!.ConfirmPossibleDuplicates);
    }

    [Fact]
    public void ReviewProgress_CountsChoices_AndCanHideChosenPlaces()
    {
        var review = Job(FavoriteDestinationImportStatus.NeedsReview, processed: 2) with
        {
            Ambiguities =
            [
                new FavoriteDestinationImportAmbiguity(1, "Starbucks", "Starbucks",
                [
                    new FavoriteDestinationPlaceCandidate("Starbucks", "1912 Pike Pl, Seattle", "Seattle", "United States", 47.61, -122.34),
                    new FavoriteDestinationPlaceCandidate("Starbucks", "1124 Pike St, Seattle", "Seattle", "United States", 47.6, -122.33)
                ]),
                new FavoriteDestinationImportAmbiguity(2, "Eiffel Tower", "Eiffel Tower",
                [
                    new FavoriteDestinationPlaceCandidate("Eiffel Tower", "5 Esplanade des Ouvriers de la Tour Eiffel, 75007 Paris", "Paris", "France", 48.858, 2.294),
                    new FavoriteDestinationPlaceCandidate("Eiffel Tower", "MAIN ST, Paris, KY 40361", "Paris", "United States", 38.2, -84.25)
                ])
            ]
        };
        var api = new ImportFavoriteDestinationApiClient();
        api.OpenImports.Add(review);
        var cut = RenderImport(api);

        cut.WaitForAssertion(() => Assert.Contains("0 of 2 places chosen", cut.Markup));
        cut.Find("#favorite-import-1-0").Change(true);
        Assert.Contains("1 of 2 places chosen", cut.Markup);

        cut.Find("#favorite-import-unchosen-only").Change(true);
        Assert.Empty(cut.FindAll("[data-testid='favorite-import-ambiguity-1']"));
        Assert.NotNull(cut.Find("[data-testid='favorite-import-ambiguity-2']"));

        cut.Find("#favorite-import-2-1").Change(true);
        Assert.Contains("2 of 2 places chosen", cut.Markup);
        Assert.Contains("Every place has a choice.", cut.Markup);
        Assert.False(cut.Find("#favorite-import-finish").HasAttribute("disabled"));
    }

    [Fact]
    public void Discard_RemovesTheUnfinishedImport()
    {
        var review = Job(FavoriteDestinationImportStatus.Failed) with { ErrorMessage = "The import could not be processed." };
        var api = new ImportFavoriteDestinationApiClient();
        api.OpenImports.Add(review);
        var cut = RenderImport(api);

        cut.WaitForAssertion(() => Assert.Contains("The import could not be processed.", cut.Markup));
        cut.Find("#favorite-import-discard").Click();

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#favorite-import-submit")));
        Assert.Equal(review.ImportId, api.DiscardedImportId);
    }

    private static readonly Guid ImportId = Guid.NewGuid();

    private static FavoriteDestinationImportJobDto Job(FavoriteDestinationImportStatus status, int processed = 0, int imported = 0)
        => new(ImportId, "favorites.csv", status, 2, processed, imported, null, [], [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static FavoriteDestinationImportResponse Queued(FavoriteDestinationImportJobDto job)
        => new(job, Array.Empty<FavoriteDestinationImportIssue>());

    private IRenderedComponent<FavoriteDestinationImport> RenderImport(ImportFavoriteDestinationApiClient api, Action? onImported = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IFavoriteDestinationApiClient>(api);
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        return Render<FavoriteDestinationImport>(parameters => parameters
            .Add(component => component.PollInterval, TimeSpan.FromMilliseconds(10))
            .Add(component => component.OnImported, () => onImported?.Invoke()));
    }

    private static Task SelectFileAsync(IRenderedComponent<FavoriteDestinationImport> cut, string name, string content)
        => cut.InvokeAsync(() => cut.Instance.OnFileSelected(new InputFileChangeEventArgs([new FakeBrowserFile(name, content)])));

    private sealed class FakeBrowserFile(string name, string content) : IBrowserFile
    {
        private readonly byte[] _contents = Encoding.UTF8.GetBytes(content);
        public string Name { get; } = name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => _contents.Length;
        public string ContentType => "text/plain";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        {
            if (Size > maxAllowedSize) throw new IOException("File is too large.");
            return new MemoryStream(_contents, writable: false);
        }
    }

}

internal sealed class ImportFavoriteDestinationApiClient(params FavoriteDestinationImportResponse[] responses) : IFavoriteDestinationApiClient
{
    private readonly Queue<FavoriteDestinationImportResponse> _responses = new(responses);
    public int ImportCallCount { get; private set; }
    public List<FavoriteDestinationImportJobDto> OpenImports { get; } = [];
    public Queue<FavoriteDestinationImportJobDto> PollResponses { get; } = new();
    public FavoriteDestinationImportCompletion? Completion { get; init; }
    public CompleteFavoriteDestinationImportRequest? LastCompletion { get; private set; }
    public Guid? DiscardedImportId { get; private set; }

    public Task<IReadOnlyList<FavoriteDestinationDto>> GetAsync(string? search = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FavoriteDestinationDto>>(Array.Empty<FavoriteDestinationDto>());

    public Task<FavoriteDestinationMutationResult> CreateAsync(CreateFavoriteDestinationRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<FavoriteDestinationMutationResult> UpdateAsync(Guid id, UpdateFavoriteDestinationRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<int?> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        ImportCallCount++;
        return Task.FromResult(_responses.Dequeue());
    }

    public Task<IReadOnlyList<FavoriteDestinationImportJobDto>> GetOpenImportsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<FavoriteDestinationImportJobDto>>(OpenImports);

    public Task<FavoriteDestinationImportJobDto?> GetImportAsync(Guid importId, CancellationToken ct = default)
        => Task.FromResult<FavoriteDestinationImportJobDto?>(PollResponses.Count > 1 ? PollResponses.Dequeue() : PollResponses.Peek());

    public Task<FavoriteDestinationImportCompletion> CompleteImportAsync(Guid importId, CompleteFavoriteDestinationImportRequest request, CancellationToken ct = default)
    {
        LastCompletion = request;
        return Task.FromResult(Completion ?? throw new NotSupportedException());
    }

    public Task<bool> DiscardImportAsync(Guid importId, CancellationToken ct = default)
    {
        DiscardedImportId = importId;
        return Task.FromResult(true);
    }
}