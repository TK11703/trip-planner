using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class TripMapApiClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task FailedRequest_ThrowsInsteadOfReturningEmptyMap(HttpStatusCode status)
    {
        using var http = Client(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(ApiError.NotFoundOrDenied())
        });
        var client = new TripApiClient(http);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetTripMapAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EmptySuccessfulMap_RemainsEmpty()
    {
        using var http = Client(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new TripMapResponse(Array.Empty<TripMapLocation>()))
        });

        var map = await new TripApiClient(http).GetTripMapAsync(Guid.NewGuid());

        Assert.Empty(map.Locations);
    }

    [Fact]
    public async Task NullResponse_ThrowsInsteadOfReturningEmptyMap()
    {
        using var http = Client(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new TripApiClient(http).GetTripMapAsync(Guid.NewGuid()));
    }

    private static HttpClient Client(HttpResponseMessage response)
        => new(new ResponseHandler(response)) { BaseAddress = new Uri("https://localhost") };

    private sealed class ResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
