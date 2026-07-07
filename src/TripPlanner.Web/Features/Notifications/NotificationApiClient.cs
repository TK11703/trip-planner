using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.Notifications;

namespace TripPlanner.Web.Features.Notifications;

public interface INotificationApiClient
{
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationResponse>> GetListAsync(CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid notificationId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(CancellationToken ct = default);
    Task<CompleteNotificationResponse?> CompleteAsync(Guid notificationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid notificationId, CancellationToken ct = default);
    Task<NotificationPreferencesResponse?> GetPreferencesAsync(CancellationToken ct = default);
    Task<NotificationPreferenceResponse?> UpdatePreferenceAsync(string category, UpdateNotificationPreferenceRequest request, CancellationToken ct = default);
}

public sealed class NotificationApiClient : INotificationApiClient
{
    private readonly HttpClient _http;

    public NotificationApiClient(HttpClient http) => _http = http;

    public async Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/notifications/count", ct);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        var payload = await response.Content.ReadFromJsonAsync<NotificationCountResponse>(cancellationToken: ct);
        return payload?.UnreadCount ?? 0;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetListAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/notifications", ct);
        if (!response.IsSuccessStatusCode)
        {
            return Array.Empty<NotificationResponse>();
        }

        var payload = await response.Content.ReadFromJsonAsync<NotificationListResponse>(cancellationToken: ct);
        return payload?.Items ?? (IReadOnlyList<NotificationResponse>)Array.Empty<NotificationResponse>();
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/notifications/{notificationId}/read", content: null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync("/api/notifications/read-all", content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        var payload = await response.Content.ReadFromJsonAsync<MarkAllNotificationsReadResponse>(cancellationToken: ct);
        return payload?.MarkedReadCount ?? 0;
    }

    public async Task<CompleteNotificationResponse?> CompleteAsync(Guid notificationId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/notifications/{notificationId}/complete", content: null, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<CompleteNotificationResponse>(cancellationToken: ct);
    }

    public async Task<bool> DeleteAsync(Guid notificationId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/notifications/{notificationId}", ct);
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NoContent;
    }

    public async Task<NotificationPreferencesResponse?> GetPreferencesAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/notification-preferences", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<NotificationPreferencesResponse>(cancellationToken: ct);
    }

    public async Task<NotificationPreferenceResponse?> UpdatePreferenceAsync(string category, UpdateNotificationPreferenceRequest request, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"/api/notification-preferences/{category}", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<NotificationPreferenceResponse>(cancellationToken: ct);
    }
}
