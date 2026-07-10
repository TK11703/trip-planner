using TripPlanner.Contracts.Profile;
using TripPlanner.Web.Features.Profile;

namespace TripPlanner.Web.Features.Maps;

/// <summary>
/// Supplies the current user's default mapping tool (<see cref="MapProviders"/>) to the globe
/// action. Reads the profile once per circuit and caches it, falling back to Bing when the profile
/// cannot be read. Call <see cref="Invalidate"/> after the profile is saved so a changed default
/// takes effect without a full reload.
/// </summary>
public interface IMapPreferenceProvider
{
    ValueTask<string> GetProviderAsync(CancellationToken ct = default);
    void Invalidate();
}

public sealed class MapPreferenceProvider : IMapPreferenceProvider
{
    private readonly IProfileApiClient _profileApi;
    private string? _cached;

    public MapPreferenceProvider(IProfileApiClient profileApi) => _profileApi = profileApi;

    public async ValueTask<string> GetProviderAsync(CancellationToken ct = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        try
        {
            var profile = await _profileApi.GetAsync(ct);
            _cached = MapProviders.Normalize(profile?.MapProvider);
        }
        catch
        {
            _cached = MapProviders.Bing;
        }

        return _cached;
    }

    public void Invalidate() => _cached = null;
}
