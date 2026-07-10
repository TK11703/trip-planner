using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Contracts.Places;

namespace TripPlanner.Api.Features.Places;

public static class SuggestPlacesEndpoint
{
    private const int MinimumQueryLength = 3;

    public static RouteGroupBuilder MapSuggestPlaces(this RouteGroupBuilder group)
    {
        group.MapGet("/suggest", HandleAsync).WithName("SuggestPlaces");
        return group;
    }

    private static async Task<Ok<IReadOnlyList<PlaceSuggestion>>> HandleAsync(
        string? q,
        IPlaceSuggestionLookup lookup,
        CancellationToken ct)
    {
        var query = q?.Trim() ?? string.Empty;
        if (query.Length < MinimumQueryLength)
        {
            return TypedResults.Ok<IReadOnlyList<PlaceSuggestion>>(Array.Empty<PlaceSuggestion>());
        }

        var suggestions = await lookup.SearchAsync(query, ct);
        return TypedResults.Ok(suggestions);
    }
}
