using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Features.Places;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripMaps;

public static class GetTripMapEndpoint
{
    private const int MaxLocationLength = 200;

    public static RouteGroupBuilder MapGetTripMap(this RouteGroupBuilder group)
    {
        group.MapGet("/map", HandleAsync).WithName("GetTripMap");
        return group;
    }

    private static async Task<Results<Ok<TripMapResponse>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripItemRepository items,
        IPlaceGeocoder geocoder,
        IAuditRepository audit,
        IClock clock,
        CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-map", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var trackedItems = await items.GetTrackedItemsAsync(access.OwnerUserId, tripId, ct);

        // Keep only map-capable locations, matching the globe's rule (non-empty, has a letter/digit, <=200).
        var mappable = trackedItems
            .Where(i => IsMapCapable(i.Location))
            .Select(i => new { i.TrackedItemId, i.Title, Location = i.Location!.Trim() })
            .ToArray();

        // Geocode each distinct location text once, then fan the coordinates back to each event.
        var distinct = mappable
            .Select(i => i.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var resolved = new Dictionary<string, GeoPoint>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in distinct)
        {
            var point = await geocoder.GeocodeAsync(text, ct);
            if (point is { } p)
            {
                resolved[text] = p;
            }
        }

        var locations = mappable
            .Where(i => resolved.ContainsKey(i.Location))
            .Select(i =>
            {
                var p = resolved[i.Location];
                return new TripMapLocation(i.TrackedItemId, i.Title, i.Location, p.Latitude, p.Longitude);
            })
            .ToArray();

        return TypedResults.Ok(new TripMapResponse(locations));
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
