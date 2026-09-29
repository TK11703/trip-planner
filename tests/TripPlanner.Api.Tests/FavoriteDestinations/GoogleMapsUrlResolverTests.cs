using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Api.Features.FavoriteDestinations;

namespace TripPlanner.Api.Tests.FavoriteDestinations;

public sealed class GoogleMapsUrlResolverTests
{
    [Fact]
    public async Task FullPlaceUrl_DecodesPlaceNameWithoutHttpRequest()
    {
        var handler = new RedirectHandler();
        var resolver = Create(handler);

        var result = await resolver.ResolveAsync(
            "https://www.google.com/maps/place/Galeries+Lafayette+Haussmann/data=!4m2!3m1!1s123");

        Assert.True(result.IsGoogleMapsUrl);
        Assert.Equal("Galeries Lafayette Haussmann", result.SearchText);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ShortUrl_FollowsTrustedRedirectAndDecodesPlaceName()
    {
        var handler = new RedirectHandler(
            new Uri("https://www.google.com/maps/place/Galeries+Lafayette+Haussmann/data=!4m2!3m1!1s123"));
        var resolver = Create(handler);

        var result = await resolver.ResolveAsync("https://maps.app.goo.gl/abc123");

        Assert.True(result.IsGoogleMapsUrl);
        Assert.Equal("Galeries Lafayette Haussmann", result.SearchText);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task ShortUrl_RejectsRedirectToUntrustedHost()
    {
        var handler = new RedirectHandler(new Uri("https://example.com/maps/place/Not-Google"));
        var resolver = Create(handler);

        var result = await resolver.ResolveAsync("https://maps.app.goo.gl/abc123");

        Assert.True(result.IsGoogleMapsUrl);
        Assert.Null(result.SearchText);
    }

    [Fact]
    public async Task NonMapsGoogleUrl_IsNotTreatedAsGoogleMapsLink()
    {
        var resolver = Create(new RedirectHandler());

        var result = await resolver.ResolveAsync("https://www.google.com/search?q=Louvre");

        Assert.False(result.IsGoogleMapsUrl);
        Assert.Null(result.SearchText);
    }

    private static GoogleMapsUrlResolver Create(HttpMessageHandler handler)
        => new(
            new StubHttpClientFactory(handler),
            NullLogger<GoogleMapsUrlResolver>.Instance);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RedirectHandler(params Uri[] redirects) : HttpMessageHandler
    {
        private readonly Queue<Uri> _redirects = new(redirects);
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (_redirects.TryDequeue(out var redirect))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = redirect }
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
