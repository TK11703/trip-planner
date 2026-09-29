using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Api.Features.Places;
using TripPlanner.Contracts.Places;
using Xunit;

namespace TripPlanner.Api.Tests.Places;

public class AzureMapsPlaceSuggestionLookupTests
{
    private static IConfiguration Config(string? clientId, string? countrySet = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureMaps:ClientId"] = clientId,
                ["AzureMaps:CountrySet"] = countrySet
            })
            .Build();

    private static AzureMapsPlaceSuggestionLookup Create(HttpStatusCode status, string body, string? clientId = "test-client-id")
    {
        var factory = new StubHttpClientFactory(new FakeHandler(status, body));
        return new AzureMapsPlaceSuggestionLookup(factory, Config(clientId), new StubTokenCredential(), NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);
    }

    [Fact]
    public async Task NotConfigured_ReturnsEmpty()
    {
        var lookup = new AzureMapsPlaceSuggestionLookup(
            new StubHttpClientFactory(new FakeHandler(HttpStatusCode.OK, "{}")),
            Config(clientId: null),
            new StubTokenCredential(),
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

        [Fact]
        public async Task ResolveAddress_ReturnsStructuredCityAndCountryComponents()
        {
                const string body = """
                {
                    "results": [
                        { "position": { "lat": 38.65123, "lon": -77.25321 }, "address": { "freeformAddress": "4129 Merchant Plaza, Woodbridge, VA 22192", "municipality": "Occoquan", "localName": "Woodbridge", "country": "United States", "countryCode": "US" } }
                    ]
                }
                """;
                var lookup = Create(HttpStatusCode.OK, body);

                var result = await lookup.ResolveAddressAsync("4129 Merchant Plaza, Woodbridge, VA 22192", CancellationToken.None);

                Assert.Equal(new PlaceAddressComponents("Woodbridge", "United States", 38.65123, -77.25321), result);
        }

        [Fact]
        public async Task ResolveAddress_WithoutLocalName_FallsBackToMunicipality()
        {
                const string body = """
                {
                    "results": [
                        { "address": { "freeformAddress": "10 Downing Street, London SW1A 2AA", "municipality": "London", "country": "United Kingdom" } }
                    ]
                }
                """;
                var lookup = Create(HttpStatusCode.OK, body);

                var result = await lookup.ResolveAddressAsync("10 Downing Street", CancellationToken.None);

                Assert.Equal(new PlaceAddressComponents("London", "United Kingdom", null, null), result);
        }

        [Fact]
        public async Task ResolveAddress_UnconfiguredOrUnresolved_ReturnsNull()
        {
                var unconfigured = Create(HttpStatusCode.OK, "{}", clientId: null);
                var unresolved = Create(HttpStatusCode.OK, "{ \"results\": [] }");
                var failed = Create(HttpStatusCode.ServiceUnavailable, "{}");

                Assert.Null(await unconfigured.ResolveAddressAsync("Paris", CancellationToken.None));
                Assert.Null(await unresolved.ResolveAddressAsync("Unknown place", CancellationToken.None));
                Assert.Null(await failed.ResolveAddressAsync("Paris", CancellationToken.None));
        }

    [Fact]
    public async Task SendsEntraBearerTokenAndClientId()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var lookup = new AzureMapsPlaceSuggestionLookup(
            new StubHttpClientFactory(handler),
            Config("account-unique-id"),
            new StubTokenCredential(),
            NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);

        await lookup.SearchAsync("Louvre", CancellationToken.None);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization?.Scheme);
        Assert.Equal(StubTokenCredential.Token, handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.Equal("account-unique-id", Assert.Single(handler.LastRequest.Headers.GetValues("x-ms-client-id")));
        Assert.DoesNotContain("subscription-key", handler.LastRequest.Headers.Select(h => h.Key));
    }

    private sealed class StubTokenCredential : TokenCredential
    {
        public const string Token = "stub-access-token";

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(Token, DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
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

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
