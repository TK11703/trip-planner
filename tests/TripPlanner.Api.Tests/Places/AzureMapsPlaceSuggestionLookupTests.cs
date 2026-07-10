using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Api.Features.Places;
using Xunit;

namespace TripPlanner.Api.Tests.Places;

public class AzureMapsPlaceSuggestionLookupTests
{
    private static IConfiguration Config(string? key, string? countrySet = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureMaps:SubscriptionKey"] = key,
                ["AzureMaps:CountrySet"] = countrySet
            })
            .Build();

    private static AzureMapsPlaceSuggestionLookup Create(HttpStatusCode status, string body, string? key = "test-key")
    {
        var factory = new StubHttpClientFactory(new FakeHandler(status, body));
        return new AzureMapsPlaceSuggestionLookup(factory, Config(key), NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);
    }

    [Fact]
    public async Task NotConfigured_ReturnsEmpty()
    {
        var lookup = new AzureMapsPlaceSuggestionLookup(
            new StubHttpClientFactory(new FakeHandler(HttpStatusCode.OK, "{}")),
            Config(key: null),
            NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);

        Assert.False(lookup.IsConfigured);
        Assert.Empty(await lookup.SearchAsync("Louvre", CancellationToken.None));
    }

    [Fact]
    public async Task BlankQuery_ReturnsEmpty()
    {
        var lookup = Create(HttpStatusCode.OK, "{}");
        Assert.Empty(await lookup.SearchAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task HttpFailure_ReturnsEmpty()
    {
        var lookup = Create(HttpStatusCode.Unauthorized, "{}");
        Assert.Empty(await lookup.SearchAsync("Louvre", CancellationToken.None));
    }

    [Fact]
    public async Task ParsesFreeformAddresses_AndDeduplicates()
    {
        const string body = """
        {
          "results": [
            { "address": { "freeformAddress": "Louvre Museum, 75001 Paris" } },
            { "address": { "freeformAddress": "Louvre-Rivoli, Paris" } },
            { "address": { "freeformAddress": "Louvre Museum, 75001 Paris" } }
          ]
        }
        """;
        var lookup = Create(HttpStatusCode.OK, body);

        var results = await lookup.SearchAsync("Louvre", CancellationToken.None);

        Assert.Collection(results,
            r => Assert.Equal("Louvre Museum, 75001 Paris", r.Description),
            r => Assert.Equal("Louvre-Rivoli, Paris", r.Description));
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
