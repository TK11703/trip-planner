using System.Net;
using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.FavoriteDestinations;

namespace TripPlanner.Web.Features.FavoriteDestinations;

public interface IFavoriteDestinationApiClient
{
    Task<IReadOnlyList<FavoriteDestinationDto>> GetAsync(string? search = null, CancellationToken ct = default);
    Task<FavoriteDestinationMutationResult> CreateAsync(CreateFavoriteDestinationRequest request, CancellationToken ct = default);
    Task<FavoriteDestinationMutationResult> UpdateAsync(Guid id, UpdateFavoriteDestinationRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int?> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, bool confirmPossibleDuplicates = false, CancellationToken ct = default);
}

public sealed record FavoriteDestinationMutationResult(
    FavoriteDestinationDto? Favorite,
    FavoriteDestinationDuplicateWarning? DuplicateWarning,
    string? ErrorMessage)
{
    public bool Succeeded => Favorite is not null && ErrorMessage is null;
}

public sealed class FavoriteDestinationApiClient : IFavoriteDestinationApiClient
{
    private readonly HttpClient _http;

    public FavoriteDestinationApiClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<FavoriteDestinationDto>> GetAsync(string? search = null, CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(search)
            ? "/api/favorite-destinations"
            : $"/api/favorite-destinations?q={Uri.EscapeDataString(search.Trim())}";
        var response = await _http.GetAsync(path, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FavoriteDestinationDto[]>(cancellationToken: ct)
            ?? Array.Empty<FavoriteDestinationDto>();
    }

    public async Task<FavoriteDestinationMutationResult> CreateAsync(CreateFavoriteDestinationRequest request, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/favorite-destinations", request, ct);
        return await ReadMutationResultAsync(response, ct);
    }

    public async Task<FavoriteDestinationMutationResult> UpdateAsync(Guid id, UpdateFavoriteDestinationRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/favorite-destinations/{id}", request, ct);
        return await ReadMutationResultAsync(response, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/favorite-destinations/{id}", ct);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<int?> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("/api/favorite-destinations/delete", new DeleteFavoriteDestinationsRequest(ids.ToArray()), ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<DeleteFavoriteDestinationsResponse>(cancellationToken: ct);
        return result?.DeletedCount;
    }

    public async Task<FavoriteDestinationImportResponse> ImportAsync(string fileName, Stream content, bool confirmPossibleDuplicates = false, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", Path.GetFileName(fileName));
        form.Add(new StringContent(confirmPossibleDuplicates.ToString(System.Globalization.CultureInfo.InvariantCulture)), "confirmPossibleDuplicates");

        var response = await _http.PostAsync("/api/favorite-destinations/import", form, ct);
        return await response.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>(cancellationToken: ct)
            ?? new FavoriteDestinationImportResponse(
                Array.Empty<FavoriteDestinationDto>(),
                [new FavoriteDestinationImportIssue(null, response.StatusCode == HttpStatusCode.RequestEntityTooLarge
                    ? "The selected file is too large."
                    : "The import response was empty.")],
                Array.Empty<FavoriteDestinationImportDuplicate>(),
                false);
    }

    private static async Task<FavoriteDestinationMutationResult> ReadMutationResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var favorite = await response.Content.ReadFromJsonAsync<FavoriteDestinationDto>(cancellationToken: ct);
            return new FavoriteDestinationMutationResult(favorite, null, favorite is null ? "The favorite destination response was empty." : null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var duplicateWarning = await response.Content.ReadFromJsonAsync<FavoriteDestinationDuplicateWarning>(cancellationToken: ct);
            if (duplicateWarning is not null)
            {
                return new FavoriteDestinationMutationResult(null, duplicateWarning, null);
            }
        }

        var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
        return new FavoriteDestinationMutationResult(null, null, error?.Message ?? "The favorite destination could not be saved.");
    }
}