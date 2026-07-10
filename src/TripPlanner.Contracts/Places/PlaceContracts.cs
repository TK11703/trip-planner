namespace TripPlanner.Contracts.Places;

/// <summary>
/// A single address/place suggestion returned by the location typeahead. <see cref="Description"/>
/// is a human-readable, map-capable address suitable for placing directly into a location field.
/// </summary>
public sealed record PlaceSuggestion(string Description);
