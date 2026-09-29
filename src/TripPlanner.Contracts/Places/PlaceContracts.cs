namespace TripPlanner.Contracts.Places;

/// <summary>
/// A single address/place suggestion returned by the location typeahead. <see cref="Description"/>
/// is a human-readable, map-capable address suitable for placing directly into a location field.
/// </summary>
public sealed record PlaceSuggestion(string Description);

/// <summary>City, country, and WGS84 coordinates returned by a structured address lookup.</summary>
public sealed record PlaceAddressComponents(string? City, string? Country, double? Latitude, double? Longitude);
