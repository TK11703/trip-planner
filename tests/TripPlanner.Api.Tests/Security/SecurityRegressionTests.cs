using System.Net;
using Xunit;

namespace TripPlanner.Api.Tests.Security;

public class SecurityRegressionTests : IClassFixture<TripPlanner.Api.Tests.Infrastructure.TestApiFactory>
{
    private readonly TripPlanner.Api.Tests.Infrastructure.TestApiFactory _factory;
    public SecurityRegressionTests(TripPlanner.Api.Tests.Infrastructure.TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DirectTripIdLookup_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/trips/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TimelineLookup_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/trips/{Guid.NewGuid()}/timeline");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
