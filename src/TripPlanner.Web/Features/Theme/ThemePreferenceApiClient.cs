using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.Theme;

namespace TripPlanner.Web.Features.Theme;

public interface IThemePreferenceApiClient
{
    Task<ThemePreferenceResponse?> GetAsync(CancellationToken cancellationToken = default);
    Task<ThemePreferenceResponse> SaveAsync(TripPlanner.Contracts.Theme.ThemeMode mode, CancellationToken cancellationToken = default);
}

public sealed class ThemePreferenceApiClient : IThemePreferenceApiClient
{
    private readonly HttpClient _http;

    public ThemePreferenceApiClient(HttpClient http) => _http = http;

    public async Task<ThemePreferenceResponse?> GetAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetAsync("/api/theme-preference", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ThemePreferenceResponse>(cancellationToken: cancellationToken);
    }

    public async Task<ThemePreferenceResponse> SaveAsync(TripPlanner.Contracts.Theme.ThemeMode mode, CancellationToken cancellationToken = default)
    {
        var response = await _http.PutAsJsonAsync("/api/theme-preference", new UpdateThemePreferenceRequest(mode), cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ThemePreferenceResponse>(cancellationToken: cancellationToken))!;
    }
}
