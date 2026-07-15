using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Web.Features.InboxHistory;

public interface IInboxHistoryApiClient
{
    Task<IReadOnlyList<InboxEmailDto>> GetHistoryAsync(CancellationToken ct = default);
    Task<bool> ReprocessEmailAsync(Guid inboxEmailId, CancellationToken ct = default);
}

public sealed class InboxHistoryApiClient : IInboxHistoryApiClient
{
    private readonly HttpClient _http;

    public InboxHistoryApiClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<InboxEmailDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/email-ingestion/inbox", ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<InboxEmailDto>();
        var payload = await response.Content.ReadFromJsonAsync<InboxEmailListResponse>(cancellationToken: ct);
        return payload?.Items ?? Array.Empty<InboxEmailDto>();
    }

    public async Task<bool> ReprocessEmailAsync(Guid inboxEmailId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/email-ingestion/inbox/{inboxEmailId}/reprocess", content: null, ct);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
    }
}
