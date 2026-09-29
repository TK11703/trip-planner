using System.Net;

namespace TripPlanner.Api.Features.FavoriteDestinations;

public readonly record struct GoogleMapsPlaceReference(bool IsGoogleMapsUrl, string? SearchText);

public sealed class GoogleMapsUrlResolver
{
    public const string HttpClientName = "google-maps-links";

    private const int MaximumRedirects = 5;
    private static readonly HashSet<string> FullGoogleMapsHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "google.com",
        "www.google.com",
        "maps.google.com"
    };
    private static readonly HashSet<string> ShortGoogleMapsHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "maps.app.goo.gl",
        "goo.gl"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GoogleMapsUrlResolver> _logger;

    public GoogleMapsUrlResolver(
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleMapsUrlResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GoogleMapsPlaceReference> ResolveAsync(
        string value,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !IsGoogleMapsUri(uri))
        {
            return default;
        }

        if (FullGoogleMapsHosts.Contains(uri.Host))
        {
            return new GoogleMapsPlaceReference(true, ExtractSearchText(uri));
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var current = uri;
            for (var redirectCount = 0; redirectCount < MaximumRedirects; redirectCount++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                using var response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
                {
                    return new GoogleMapsPlaceReference(true, ExtractSearchText(current));
                }

                var next = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(current, response.Headers.Location);
                if (next.Scheme != Uri.UriSchemeHttps || !IsGoogleMapsHost(next.Host))
                {
                    _logger.LogWarning(
                        "Rejected Google Maps redirect to untrusted host {RedirectHost}.",
                        next.Host);
                    return new GoogleMapsPlaceReference(true, null);
                }

                current = next;
                if (FullGoogleMapsHosts.Contains(current.Host))
                {
                    return new GoogleMapsPlaceReference(true, ExtractSearchText(current));
                }
            }

            _logger.LogWarning("Google Maps link exceeded the maximum redirect count.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogWarning(
                "Google Maps link resolution timed out; falling back to the imported title. Error type: {ErrorType}",
                exception.GetType().Name);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                "Google Maps link resolution failed; falling back to the imported title. Error type: {ErrorType}",
                exception.GetType().Name);
        }

        return new GoogleMapsPlaceReference(true, null);
    }

    private static bool IsGoogleMapsHost(string host)
        => FullGoogleMapsHosts.Contains(host) || ShortGoogleMapsHosts.Contains(host);

    private static bool IsGoogleMapsUri(Uri uri)
        => ShortGoogleMapsHosts.Contains(uri.Host)
            || FullGoogleMapsHosts.Contains(uri.Host)
                && (uri.Host.Equals("maps.google.com", StringComparison.OrdinalIgnoreCase)
                    || uri.AbsolutePath.Equals("/maps", StringComparison.OrdinalIgnoreCase)
                    || uri.AbsolutePath.StartsWith("/maps/", StringComparison.OrdinalIgnoreCase));

    private static bool IsRedirect(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static string? ExtractSearchText(Uri uri)
    {
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("place", StringComparison.OrdinalIgnoreCase))
            {
                return Decode(segments[index + 1]);
            }
        }

        foreach (var parameterName in new[] { "query", "destination", "q" })
        {
            var value = ReadQueryParameter(uri.Query, parameterName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return Decode(value);
            }
        }

        return null;
    }

    private static string? ReadQueryParameter(string query, string parameterName)
    {
        foreach (var parameter in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = parameter.Split('=', 2);
            if (parts[0].Equals(parameterName, StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length == 2 ? parts[1] : null;
            }
        }

        return null;
    }

    private static string? Decode(string value)
    {
        try
        {
            var decoded = Uri.UnescapeDataString(value.Replace('+', ' ')).Trim();
            return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
