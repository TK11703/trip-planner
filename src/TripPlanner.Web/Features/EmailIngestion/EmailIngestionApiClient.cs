using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Web.Features.EmailIngestion;

public interface IEmailIngestionApiClient
{
    Task<IReadOnlyList<ParsedItemDraftDto>> GetDraftsAsync(CancellationToken ct = default);
    Task<ParsedItemDraftDto?> UpdateDraftAsync(Guid draftId, UpdateParsedItemDraftRequest request, CancellationToken ct = default);
    Task<ConfirmParsedItemDraftResponse?> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default);
    Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default);
}

public sealed class EmailIngestionApiClient : IEmailIngestionApiClient
{
    private readonly HttpClient _http;

    public EmailIngestionApiClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ParsedItemDraftDto>> GetDraftsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/email-ingestion/drafts", ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<ParsedItemDraftDto>();
        var payload = await response.Content.ReadFromJsonAsync<ParsedItemDraftListResponse>(cancellationToken: ct);
        return payload?.Items ?? Array.Empty<ParsedItemDraftDto>();
    }

    public async Task<ParsedItemDraftDto?> UpdateDraftAsync(Guid draftId, UpdateParsedItemDraftRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ParsedItemDraftDto>(cancellationToken: ct);
    }

    public async Task<ConfirmParsedItemDraftResponse?> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", content: null, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ConfirmParsedItemDraftResponse>(cancellationToken: ct);
    }

    public async Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/email-ingestion/drafts/{draftId}/discard", content: null, ct);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
    }
}
