using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.Errors;

namespace TripPlanner.Web.Features.EmailIngestion;

/// <summary>
/// The outcome of a draft save or confirm. A refusal carries the API's own field-level reason so
/// the review queue can show it against the control that caused it, letting the traveler correct
/// it without leaving the queue (FR-022).
/// </summary>
public sealed record DraftMutationResult<T>(T? Value, ApiError? Error) where T : class
{
    public bool Succeeded => Error is null && Value is not null;

    /// <summary>The name of the offending input, when the API named one.</summary>
    public string? Field => Error?.Details is { } d && d.TryGetValue("field", out var name) ? name : null;
}

public interface IEmailIngestionApiClient
{
    Task<IReadOnlyList<ParsedItemDraftDto>> GetDraftsAsync(CancellationToken ct = default);
    Task<DraftMutationResult<ParsedItemDraftDto>> UpdateDraftAsync(Guid draftId, UpdateParsedItemDraftRequest request, CancellationToken ct = default);
    Task<DraftMutationResult<ConfirmParsedItemDraftResponse>> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default);
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

    public async Task<DraftMutationResult<ParsedItemDraftDto>> UpdateDraftAsync(Guid draftId, UpdateParsedItemDraftRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/email-ingestion/drafts/{draftId}", request, ct);
        return await ReadResultAsync<ParsedItemDraftDto>(response, "We couldn't save your changes. Please try again.", ct);
    }

    public async Task<DraftMutationResult<ConfirmParsedItemDraftResponse>> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/email-ingestion/drafts/{draftId}/confirm", content: null, ct);
        return await ReadResultAsync<ConfirmParsedItemDraftResponse>(response, "We couldn't add this item. Please try again.", ct);
    }

    private static async Task<DraftMutationResult<T>> ReadResultAsync<T>(
        HttpResponseMessage response, string fallbackMessage, CancellationToken ct) where T : class
    {
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            return value is not null
                ? new DraftMutationResult<T>(value, null)
                : new DraftMutationResult<T>(null, ApiError.ValidationFailed(fallbackMessage));
        }

        // A 400 carries the field the API refused; anything else is reported in general terms.
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            ApiError? error = null;
            try { error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException) { }
            if (error is not null) return new DraftMutationResult<T>(null, error);
        }

        return new DraftMutationResult<T>(null, ApiError.ValidationFailed(fallbackMessage));
    }

    public async Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/email-ingestion/drafts/{draftId}/discard", content: null, ct);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
    }
}
