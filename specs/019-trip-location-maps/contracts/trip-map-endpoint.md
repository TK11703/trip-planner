# Contract: Trip Map Endpoint

A new read-only Minimal API vertical slice that returns the geocoded, plottable locations for a trip. Used by the built-in map modal.

## Route

```
GET /api/trips/{tripId:guid}/map
```

- **Auth**: requires an authenticated user (same as other trip endpoints).
- **Scope**: trip-owner scoped. A non-owner (or unknown trip) receives the same not-found/denied result as other `GET /api/trips/{tripId}/...` endpoints and no location data.

## Response types (`TripPlanner.Contracts/Trips/TripMapContracts.cs`)

```csharp
public sealed record TripMapResponse(IReadOnlyList<TripMapLocation> Locations);

public sealed record TripMapLocation(
    Guid TrackedItemId,
    string Title,
    string Location,     // free text as entered
    double Latitude,
    double Longitude);
```

## Behavior

1. Load the trip (owner-scoped). If not accessible → 404 (consistent with existing trip reads).
2. Select the trip's tracked items whose `Location` is map-capable (non-empty, contains a letter/digit, ≤200 chars).
3. Deduplicate distinct location texts; geocode each once via the Azure Maps geocoding capability (top result's `position`).
4. For each map-capable event whose text resolved, emit a `TripMapLocation` (fanning shared coordinates back to each event that used that text).
5. Omit events whose text did not resolve. Return `200 OK` with a possibly-empty `Locations` list.

## Degradation

| Condition | Result |
|-----------|--------|
| Azure Maps not configured (`IsConfigured == false`) | `200 OK` with empty `Locations` (the modal shows an empty state). |
| Azure Maps call fails / times out | Treated as unresolved for the affected texts; other resolved locations still returned. |
| Trip has no map-capable events | `200 OK` with empty `Locations` (note: the client normally disables the launch button in this case). |

## Geocoding capability (`TripPlanner.Api/Features/Places/PlaceSuggestionLookup.cs`)

Extend the existing Azure Maps lookup with a coordinate resolver that reuses the same HTTP client, `subscription-key` header, and `AzureMaps:CountrySet`:

```csharp
public interface IPlaceGeocoder
{
    bool IsConfigured { get; }
    Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct);   // top result position, or null
}

public readonly record struct GeoPoint(double Latitude, double Longitude);
```

- Calls Azure Maps Search fuzzy (`search/fuzzy/json?api-version=1.0&limit=1&query=...`) and reads `results[0].position.lat/lon`.
- Returns `null` on no result, non-success status, missing key, or exception (never throws to the endpoint).
- May be implemented on the existing `AzureMapsPlaceSuggestionLookup` class (which already holds the key and client) or a sibling type sharing the same `HttpClientName`.

## Web client (`ITripApiClient`)

```csharp
Task<TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default);
```

## Tests

- Owner receives resolved locations; unresolved texts are omitted; shared texts fan out to multiple events.
- Non-owner / unknown trip → not found, no data leaked.
- Not-configured and provider-failure → empty/partial `Locations`, `200 OK`.
- Geocoder unit tests: parses `position`, returns null on no result / failure / blank query / not configured (mirroring the existing suggestion-lookup tests).
