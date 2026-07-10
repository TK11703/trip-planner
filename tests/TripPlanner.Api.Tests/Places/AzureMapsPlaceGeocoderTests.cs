using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Api.Features.Places;
using Xunit;

namespace TripPlanner.Api.Tests.Places;

public class AzureMapsPlaceGeocoderTests
{
    private static IConfiguration Config(string? key)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureMaps:SubscriptionKey"] = key })
            .Build();

    private static AzureMapsPlaceSuggestionLookup Create(HttpStatusCode status, string body, string? key = "test-key")
        => new(new StubHttpClientFactory(new FakeHandler(status, body)), Config(key), NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);

    [Fact]
    public async Task NotConfigured_ReturnsNull()
    {
        var geocoder = (IPlaceGeocoder)Create(HttpStatusCode.OK, "{}", key: null);
        Assert.False(geocoder.IsConfigured);
        Assert.Null(await geocoder.GeocodeAsync("Louvre", CancellationToken.None));
    }

    [Fact]
    public async Task BlankQuery_ReturnsNull()
    {
        var geocoder = (IPlaceGeocoder)Create(HttpStatusCode.OK, "{}");
        Assert.Null(await geocoder.GeocodeAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task HttpFailure_ReturnsNull()
    {
        var geocoder = (IPlaceGeocoder)Create(HttpStatusCode.Unauthorized, "{}");
        Assert.Null(await geocoder.GeocodeAsync("Louvre", CancellationToken.None));
    }

    [Fact]
    public async Task NoResults_ReturnsNull()
    {
        var geocoder = (IPlaceGeocoder)Create(HttpStatusCode.OK, """{ "results": [] }""");
        Assert.Null(await geocoder.GeocodeAsync("Nowhere at all", CancellationToken.None));
    }

    [Fact]
    public async Task ParsesTopResultPosition()
    {
        const string body = """
        {
          "results": [
            { "position": { "lat": 48.8606, "lon": 2.3376 } },
            { "position": { "lat": 1.0, "lon": 2.0 } }
          ]
        }
        """;
        var geocoder = (IPlaceGeocoder)Create(HttpStatusCode.OK, body);

        var point = await geocoder.GeocodeAsync("Louvre Museum, Paris", CancellationToken.None);

        Assert.NotNull(point);
        Assert.Equal(48.8606, point!.Value.Latitude, 4);
        Assert.Equal(2.3376, point.Value.Longitude, 4);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler) { BaseAddress = new Uri("https://atlas.microsoft.com/") };
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public FakeHandler(HttpStatusCode status, string body) { _status = status; _body = body; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }
}
