using System.Net;
using System.Net.Http.Json;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Tests.Infrastructure;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// The relay endpoint is reachable only by a daemon caller holding the relay application role.
/// A rejected call must leave no trace in storage.
/// </summary>
public sealed class RelayIngestionAuthorizationTests : IDisposable
{
    private readonly EmailIngestionApiFactory _factory = new();

    [Fact]
    public async Task AnonymousCallIsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.Emails.Rows);
        Assert.Empty(_factory.Drafts.Rows);
    }

    [Fact]
    public async Task AuthenticatedCallerWithoutTheRelayRoleIsForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, "some-daemon");

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Emails.Rows);
    }

    [Fact]
    public async Task SignedInTravelerCannotPostToTheRelayEndpoint()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, EmailIngestionApiFactory.TravelerUserId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestEmailHeader, EmailIngestionApiFactory.TravelerEmail);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestScopeHeader, "access_as_user");

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Emails.Rows);
    }

    [Fact]
    public async Task AnUnrelatedApplicationRoleDoesNotGrantAccess()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, "other-daemon");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Reports.Export");

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TheRelayRoleAmongOthersGrantsAccess()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, "relay-application-id");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, $"Reports.Export {EmailIngestionPolicy.RelayAppRole}");

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
