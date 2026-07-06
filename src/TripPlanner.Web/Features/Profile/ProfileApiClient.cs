using System.Net.Http.Json;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Web.Features.Profile;

public interface IProfileApiClient
{
    Task<UserProfileResponse?> GetAsync(CancellationToken ct = default);
    Task<UserProfileResponse> SaveAsync(UpdateUserProfileRequest request, CancellationToken ct = default);
}

public sealed class ProfileApiClient : IProfileApiClient
{
    private readonly HttpClient _http;

    public ProfileApiClient(HttpClient http) => _http = http;

    public async Task<UserProfileResponse?> GetAsync(CancellationToken ct = default)
    {
        var resp = await _http.GetAsync("/api/profile", ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<UserProfileResponse>(cancellationToken: ct);
    }

    public async Task<UserProfileResponse> SaveAsync(UpdateUserProfileRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync("/api/profile", request, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<UserProfileResponse>(cancellationToken: ct))!;
    }
}
