using System.Net.Http.Json;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Web.Features.Trips;

public interface ITripApiClient
{
    Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default);
    Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default);
    Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default);
    Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default);
    Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default);

    Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default);
    Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default);
    Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default);
    Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default);

    Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default);
    Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default);
    Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default);

    Task<TimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default);
}

public sealed class TripApiClient : ITripApiClient
{
    private readonly HttpClient _http;
    public TripApiClient(HttpClient http) => _http = http;

    public async Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<TripListResponse>($"/api/trips?page={page}&pageSize={pageSize}", ct);
        return result ?? new TripListResponse(Array.Empty<TripSummary>(), page, pageSize, 0);
    }

    public async Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default)
    {
        var path = limit is null ? "/api/trips/recent" : $"/api/trips/recent?limit={limit}";
        var result = await _http.GetFromJsonAsync<TripSummary[]>(path, ct);
        return result ?? Array.Empty<TripSummary>();
    }

    public async Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/trips/{tripId}", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<TripDetail>(cancellationToken: ct);
    }

    public async Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("/api/trips", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CreateTripResponse>(cancellationToken: ct))!;
    }

    public async Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync($"/api/trips/{tripId}", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CreateTripResponse>(cancellationToken: ct))!;
    }

    public async Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default)
        => (await _http.PostAsJsonAsync($"/api/trips/{tripId}/legs", request, ct)).EnsureSuccessStatusCode();

    public async Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default)
        => (await _http.PutAsJsonAsync($"/api/trips/{tripId}/legs/{tripLegId}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default)
        => (await _http.DeleteAsync($"/api/trips/{tripId}/legs/{tripLegId}", ct)).EnsureSuccessStatusCode();

    public async Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/trips/{tripId}/legs/defaults", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<TripLegDefaultsResponse>(cancellationToken: ct);
    }

    public async Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default)
        => (await _http.PostAsJsonAsync($"/api/trips/{tripId}/items", request, ct)).EnsureSuccessStatusCode();

    public async Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default)
        => (await _http.PutAsJsonAsync($"/api/trips/{tripId}/items/{trackedItemId}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default)
        => (await _http.DeleteAsync($"/api/trips/{tripId}/items/{trackedItemId}", ct)).EnsureSuccessStatusCode();

    public async Task<TimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync($"/api/trips/{tripId}/timeline", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<TimelineResponse>(cancellationToken: ct);
    }
}
