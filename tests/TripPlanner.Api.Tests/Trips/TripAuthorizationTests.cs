using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Database.Connections;
using Xunit;

namespace TripPlanner.Api.Tests.Trips;

public class TripAuthorizationTests : IClassFixture<TripPlanner.Api.Tests.Infrastructure.TestApiFactory>
{
    private readonly TestApiFactory _factory;
    public TripAuthorizationTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetRecentTrips_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/trips/recent");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTripDetail_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/trips/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
