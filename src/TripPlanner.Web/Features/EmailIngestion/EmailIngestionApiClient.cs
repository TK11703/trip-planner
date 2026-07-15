using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Web.Features.EmailIngestion;

public interface IEmailIngestionApiClient
{
    Task<IReadOnlyList<ParsedEventDraftDto>> GetDraftsAsync(CancellationToken ct = default);
    Task<ParsedEventDraftDto?> UpdateDraftAsync(Guid draftId, UpdateParsedEventDraftRequest request, CancellationToken ct = default);
    Task<ConfirmParsedEventDraftResponse?> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default);
    Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default);
}

public sealed class EmailIngestionApiClient : IEmailIngestionApiClient
{
    private readonly HttpClient _http;

    public EmailIngestionApiClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ParsedEventDraftDto>> GetDraftsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/email-ingestion/drafts", ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<ParsedEventDraftDto>();
        var payload = await response.Content.ReadFromJsonAsync<ParsedEventDraftListResponse>(cancellationToken: ct);
        return payload?.Items ?? (IReadOnlyList<ParsedEventDraftDto>)Array.Empty<ParsedEventDraftDto>();
    }

    public async Task<ParsedEventDraftDto?> UpdateDraftAsync(Guid draftId, UpdateParsedEventDraftRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ParsedEventDraftDto>(cancellationToken: ct);
    }

    public async Task<ConfirmParsedEventDraftResponse?> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", content: null, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ConfirmParsedEventDraftResponse>(cancellationToken: ct);
    }

    public async Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/email-ingestion/drafts/{draftId}/discard", content: null, ct);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
    }
}
