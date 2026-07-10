using System.Net.Http.Headers;
using System.Text.Json;
using TripPlanner.Contracts.Places;

namespace TripPlanner.Api.Features.Places;

/// <summary>
/// Provides address/place suggestions for the location typeahead. Backed by Azure Maps Search
/// (fuzzy, typeahead mode). The subscription key is read from environment-driven configuration
/// (<c>AzureMaps:SubscriptionKey</c>, e.g. user-secrets or Key Vault) and never embedded. When the
/// key is missing or a call fails, the lookup degrades to an empty result so the form keeps working.
/// </summary>
public interface IPlaceSuggestionLookup
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(string query, CancellationToken ct);
}

/// <summary>A resolved geographic point (WGS84).</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// Resolves free-text location text to coordinates for the built-in trip map. Backed by Azure Maps
/// Search and degrades to <c>null</c> (never throws) when the key is missing, the query is blank,
/// the call fails, or no result is found.
/// </summary>
public interface IPlaceGeocoder
{
    bool IsConfigured { get; }
    Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct);
}

public sealed class AzureMapsPlaceSuggestionLookup : IPlaceSuggestionLookup, IPlaceGeocoder
{
    public const string HttpClientName = "azuremaps";

    private const int MaxResults = 6;

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AzureMapsPlaceSuggestionLookup> _logger;
    private readonly string? _subscriptionKey;
    private readonly string? _countrySet;

    public AzureMapsPlaceSuggestionLookup(
        IHttpClientFactory httpFactory,
        IConfiguration configuration,
        ILogger<AzureMapsPlaceSuggestionLookup> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
        _subscriptionKey = configuration["AzureMaps:SubscriptionKey"];
        // Optional ISO country codes (e.g. "US,CA") to bias/limit results. Empty means worldwide.
        _countrySet = configuration["AzureMaps:CountrySet"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_subscriptionKey);

    public async Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(string query, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PlaceSuggestion>();
        }

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);

            var term = Uri.EscapeDataString(query.Trim());
            var requestUri = $"search/fuzzy/json?api-version=1.0&typeahead=true&limit={MaxResults}&query={term}";
            if (!string.IsNullOrWhiteSpace(_countrySet))
            {
                requestUri += $"&countrySet={Uri.EscapeDataString(_countrySet)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            // The subscription key travels as a header (kept out of URLs/logs).
            request.Headers.TryAddWithoutValidation("subscription-key", _subscriptionKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Azure Maps search returned {StatusCode} for a place suggestion. A 401/403 usually means AzureMaps:SubscriptionKey is missing or invalid.",
                    (int)response.StatusCode);
                return Array.Empty<PlaceSuggestion>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<PlaceSuggestion>();
            }

            var suggestions = new List<PlaceSuggestion>(results.GetArrayLength());
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in results.EnumerateArray())
            {
                if (result.TryGetProperty("address", out var address)
                    && address.TryGetProperty("freeformAddress", out var freeform)
                    && freeform.ValueKind == JsonValueKind.String)
                {
                    var text = freeform.GetString();
                    if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
                    {
                        suggestions.Add(new PlaceSuggestion(text));
                    }
                }
            }
            return suggestions;
        }
        catch (OperationCanceledException)
        {
            // The suggestion request was superseded by a newer keystroke (the client cancels the
            // previous request) or the caller disconnected. Return no suggestions quietly and keep
            // the cancellation handled here so the debugger does not break on a benign cancel.
            return Array.Empty<PlaceSuggestion>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Maps place suggestion lookup failed.");
            return Array.Empty<PlaceSuggestion>();
        }
    }

    public async Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        try
        {
            var http = _httpFactory.CreateClient(HttpClientName);

            var term = Uri.EscapeDataString(query.Trim());
            var requestUri = $"search/fuzzy/json?api-version=1.0&limit=1&query={term}";
            if (!string.IsNullOrWhiteSpace(_countrySet))
            {
                requestUri += $"&countrySet={Uri.EscapeDataString(_countrySet)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.TryAddWithoutValidation("subscription-key", _subscriptionKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Azure Maps geocode returned {StatusCode}. A 401/403 usually means AzureMaps:SubscriptionKey is missing or invalid.",
                    (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!document.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array
                || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            if (first.TryGetProperty("position", out var position)
                && position.TryGetProperty("lat", out var lat)
                && position.TryGetProperty("lon", out var lon)
                && lat.ValueKind == JsonValueKind.Number
                && lon.ValueKind == JsonValueKind.Number)
            {
                return new GeoPoint(lat.GetDouble(), lon.GetDouble());
            }

            return null;
        }
        catch (OperationCanceledException)
        {
            // Benign cancellation (superseded request or disconnect); handled here so the debugger
            // does not break on a user-unhandled cancel.
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Maps geocode failed.");
            return null;
        }
    }
}
