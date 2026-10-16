using System.Collections.Concurrent;
using TripPlanner.Api.Features.Places;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Api.Features.TripMaps;

public sealed class TripMapLocationResolver(IPlaceGeocoder geocoder)
{
    private const int MaxLocationLength = 200;
    private const int MaxConcurrentLookups = 4;

    public async Task<TripMapResponse> ResolveAsync(IReadOnlyList<TrackedItemDto> items, CancellationToken ct)
    {
        var mappable = items
            .Where(i => IsMapCapable(i.Location))
            .Select(i => new { i.TrackedItemId, i.Title, Location = i.Location!.Trim() })
            .ToArray();
        var distinct = mappable.Select(i => i.Location).Distinct(StringComparer.OrdinalIgnoreCase);
        var resolved = new ConcurrentDictionary<string, GeoPoint>(StringComparer.OrdinalIgnoreCase);

        await Parallel.ForEachAsync(distinct, new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxConcurrentLookups,
            CancellationToken = ct
        }, async (text, token) =>
        {
            var point = await geocoder.GeocodeAsync(text, token);
            if (point is { } p)
            {
                resolved[text] = p;
            }
        });

        var locations = mappable
            .Where(i => resolved.ContainsKey(i.Location))
            .Select(i =>
            {
                var p = resolved[i.Location];
                return new TripMapLocation(i.TrackedItemId, i.Title, i.Location, p.Latitude, p.Longitude);
            })
            .ToArray();

        return new TripMapResponse(locations);
    }

    private static bool IsMapCapable(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return false;
        }

        var trimmed = location.Trim();
        return trimmed.Length <= MaxLocationLength && trimmed.Any(char.IsLetterOrDigit);
    }
}
