namespace TripPlanner.Contracts.Trips;

/// <summary>
/// The plottable locations for a trip's built-in map. Produced on demand by geocoding the trip's
/// item location text; never persisted. May be empty when the trip has no resolvable locations
/// or when the geocoding provider is unconfigured/unavailable.
/// </summary>
public sealed record TripMapResponse(IReadOnlyList<TripMapLocation> Locations);

/// <summary>
/// A single resolved point on the trip map. Carries the source item's id and title so a marker
/// can identify and open that item.
/// </summary>
public sealed record TripMapLocation(
    Guid TrackedItemId,
    string Title,
    string Location,
    double Latitude,
    double Longitude);
