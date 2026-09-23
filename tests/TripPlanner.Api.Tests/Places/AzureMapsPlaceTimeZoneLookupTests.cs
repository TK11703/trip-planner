using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Api.Features.Places;
using Xunit;

namespace TripPlanner.Api.Tests.Places;

/// <summary>
/// Email ingestion leans on this to fill a zone the language model left out, so every failure mode
/// has to come back as "no zone" rather than an exception — a throw here would turn a recoverable
/// draft into a failed ingestion.
/// </summary>
public class AzureMapsPlaceTimeZoneLookupTests
{
    private const string GeocodeBody = """
    { "results": [ { "position": { "lat": 51.5194, "lon": -0.127 } } ] }
    """;

    private static IConfiguration Config(string? clientId)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureMaps:ClientId"] = clientId })
            .Build();

    private static (IPlaceTimeZoneLookup Lookup, RoutingHandler Handler) Create(
        HttpStatusCode timeZoneStatus,
        string timeZoneBody,
        string geocodeBody = GeocodeBody,
        string? clientId = "test-client-id")
    {
        var handler = new RoutingHandler(geocodeBody, timeZoneStatus, timeZoneBody);
        var lookup = new AzureMapsPlaceSuggestionLookup(
            new StubHttpClientFactory(handler),
            Config(clientId),
            new StubTokenCredential(),
            NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);
        return (lookup, handler);
    }

    [Fact]
    public async Task NotConfigured_ReturnsNullWithoutCallingMaps()
    {
        var (lookup, handler) = Create(HttpStatusCode.OK, "{}", clientId: null);

        Assert.False(lookup.IsConfigured);
        Assert.Null(await lookup.ResolveTimeZoneAsync("British Museum, London, UK", CancellationToken.None));
        Assert.Empty(handler.RequestedUris);
    }

    [Fact]
    public async Task UnplaceableLocation_ReturnsNullWithoutAskingForAZone()
    {
        var (lookup, handler) = Create(HttpStatusCode.OK, "{}", geocodeBody: """{ "results": [] }""");

        Assert.Null(await lookup.ResolveTimeZoneAsync("somewhere nice", CancellationToken.None));
        Assert.DoesNotContain(handler.RequestedUris, uri => uri.Contains("timezone", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvesTheZoneCoveringTheGeocodedCoordinates()
    {
        const string body = """
        {
          "Version": "2025b",
          "TimeZones": [ { "Id": "Europe/London" } ]
        }
        """;
        var (lookup, handler) = Create(HttpStatusCode.OK, body);

        var zone = await lookup.ResolveTimeZoneAsync("British Museum, London, UK", CancellationToken.None);

        Assert.Equal("Europe/London", zone);
        var timeZoneRequest = Assert.Single(handler.RequestedUris, uri => uri.Contains("timezone", StringComparison.Ordinal));
        Assert.Contains("51.5194,-0.127", Uri.UnescapeDataString(timeZoneRequest), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACamelCasedResponseIsStillRead()
    {
        var (lookup, _) = Create(HttpStatusCode.OK, """{ "timeZones": [ { "id": "America/Denver" } ] }""");

        Assert.Equal("America/Denver", await lookup.ResolveTimeZoneAsync("Denver, CO", CancellationToken.None));
    }

    [Fact]
    public async Task NoZoneInTheResponse_ReturnsNull()
    {
        var (lookup, _) = Create(HttpStatusCode.OK, """{ "TimeZones": [] }""");

        Assert.Null(await lookup.ResolveTimeZoneAsync("British Museum, London, UK", CancellationToken.None));
    }

    /// <summary>
    /// The timezone API is outside the Azure Maps Search and Render Data Reader role, so a missing
    /// or too-narrow role assignment shows up here rather than on the search path.
    /// </summary>
    [Fact]
    public async Task ForbiddenFromTheTimeZoneApi_ReturnsNull()
    {
        var (lookup, _) = Create(HttpStatusCode.Forbidden, "{}");

        Assert.Null(await lookup.ResolveTimeZoneAsync("British Museum, London, UK", CancellationToken.None));
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new("stub-access-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler) { BaseAddress = new Uri("https://atlas.microsoft.com/") };
    }

    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly string _geocodeBody;
        private readonly HttpStatusCode _timeZoneStatus;
        private readonly string _timeZoneBody;

        public RoutingHandler(string geocodeBody, HttpStatusCode timeZoneStatus, string timeZoneBody)
        {
            _geocodeBody = geocodeBody;
            _timeZoneStatus = timeZoneStatus;
            _timeZoneBody = timeZoneBody;
        }

        public List<string> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            RequestedUris.Add(uri);

            var isTimeZone = uri.Contains("timezone", StringComparison.Ordinal);
            var status = isTimeZone ? _timeZoneStatus : HttpStatusCode.OK;
            var body = isTimeZone ? _timeZoneBody : _geocodeBody;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
