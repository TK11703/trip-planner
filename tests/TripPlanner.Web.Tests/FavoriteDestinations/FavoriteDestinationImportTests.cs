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
            Array.Empty<FavoriteDestinationDto>(),
            [new FavoriteDestinationImportIssue(3, "Address is required.")],
            Array.Empty<FavoriteDestinationImportDuplicate>(),
            false));
        var cut = RenderImport(api);
        await SelectFileAsync(cut, "favorites.csv", "name,address,notes\r\nMuseum,,");
        cut.Find("#favorite-import-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Row 3: Address is required.", cut.Markup));
        Assert.DoesNotContain("destinations imported", cut.Markup);
        Assert.Equal(1, api.ImportCallCount);
    }

    [Fact]
    public async Task DuplicateWarning_RequiresExplicitConfirmation()
    {
        var duplicate = new FavoriteDestinationImportDuplicate(2, "Museum", "Berlin");
        var api = new ImportFavoriteDestinationApiClient(
            new FavoriteDestinationImportResponse(Array.Empty<FavoriteDestinationDto>(), Array.Empty<FavoriteDestinationImportIssue>(), [duplicate], true),
            new FavoriteDestinationImportResponse([Favorite("Museum", "Berlin")], Array.Empty<FavoriteDestinationImportIssue>(), Array.Empty<FavoriteDestinationImportDuplicate>(), false));
        var cut = RenderImport(api);
        await SelectFileAsync(cut, "favorites.json", "[{\"name\":\"Museum\",\"address\":\"Berlin\"}]");
        cut.Find("#favorite-import-submit").Click();

        cut.WaitForAssertion(() => Assert.Contains("possible duplicates", cut.Markup, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, api.ImportCallCount);
        cut.Find("#favorite-import-confirm-duplicates").Click();

        cut.WaitForAssertion(() => Assert.Contains("1 destination imported", cut.Markup));
        Assert.Equal(2, api.ImportCallCount);
        Assert.True(api.LastConfirmPossibleDuplicates);
    }

    private IRenderedComponent<FavoriteDestinationImport> RenderImport(ImportFavoriteDestinationApiClient api)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IFavoriteDestinationApiClient>(api);
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(isAuthenticated: true));
        return Render<FavoriteDestinationImport>();
    }

    private static Task SelectFileAsync(IRenderedComponent<FavoriteDestinationImport> cut, string name, string content)
        => cut.InvokeAsync(() => cut.Instance.OnFileSelected(new InputFileChangeEventArgs([new FakeBrowserFile(name, content)])));

    private static FavoriteDestinationDto Favorite(string name, string address)
        => new(Guid.NewGuid(), name, address, null, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

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
    public bool LastConfirmPossibleDuplicates { get; private set; }

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

    public Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, bool confirmPossibleDuplicates = false, CancellationToken ct = default)
    {
        ImportCallCount++;
        LastConfirmPossibleDuplicates = confirmPossibleDuplicates;
        return Task.FromResult(_responses.Dequeue());
    }
}