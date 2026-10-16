using System.Net;
using System.Text;
using Azure.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Api.Features.Places;
using Xunit;

namespace TripPlanner.Api.Tests.Places;

public class AzureMapsPlaceGeocoderTests
{
    private static IConfiguration Config(string? clientId)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureMaps:ClientId"] = clientId })
            .Build();

    private static AzureMapsPlaceSuggestionLookup Create(HttpStatusCode status, string body, string? clientId = "test-client-id")
        => new(new StubHttpClientFactory(new FakeHandler(status, body)), Config(clientId), new StubTokenCredential(), NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);

    [Fact]
    public async Task NotConfigured_ReturnsNull()
    {
        var geocoder = (IPlaceGeocoder)Create(HttpStatusCode.OK, "{}", clientId: null);
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

    [Fact]
    public async Task ConcurrentGeocoding_SharesOneTokenAcquisition()
    {
        var credential = new CountingTokenCredential();
        using var geocoder = new AzureMapsPlaceSuggestionLookup(
            new StubHttpClientFactory(new FakeHandler(HttpStatusCode.OK,
                """{"results":[{"position":{"lat":20,"lon":-155}}]}""")),
            Config("test-client-id"), credential, NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);

        var lookups = Enumerable.Range(0, 12)
            .Select(i => geocoder.GeocodeAsync($"Place {i}", CancellationToken.None)).ToArray();
        await credential.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        credential.Release.SetResult();
        var points = await Task.WhenAll(lookups).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, credential.Calls);
        Assert.All(points, point => Assert.Equal(new GeoPoint(20, -155), point));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenNeedingRefresh_IsReacquired(bool refreshOnElapsed)
    {
        var credential = new CountingTokenCredential();
        credential.Release.SetResult();
        credential.Token = refreshOnElapsed
            ? new AccessToken("stub", DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddMinutes(-1))
            : new AccessToken("stub", DateTimeOffset.UtcNow.AddMinutes(1));
        using var geocoder = new AzureMapsPlaceSuggestionLookup(
            new StubHttpClientFactory(new FakeHandler(HttpStatusCode.OK, """{"results":[]}""")),
            Config("test-client-id"), credential, NullLogger<AzureMapsPlaceSuggestionLookup>.Instance);

        await geocoder.GeocodeAsync("First place", CancellationToken.None);
        credential.Token = new AccessToken("fresh", DateTimeOffset.UtcNow.AddHours(1));
        await geocoder.GeocodeAsync("Second place", CancellationToken.None);
        await geocoder.GeocodeAsync("Third place", CancellationToken.None);

        Assert.Equal(2, credential.Calls);
    }

    private sealed class CountingTokenCredential : TokenCredential
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public AccessToken Token { get; set; } = new("stub", DateTimeOffset.UtcNow.AddHours(1));
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Token;
        }
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
