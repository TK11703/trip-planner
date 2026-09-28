using System.Net;
using System.Net.Http.Json;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.TripDataChat;

namespace TripPlanner.Api.Tests.TripDataChat;

public sealed class TripDataChatEndpointTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task AnonymousRequestIsRejectedBeforeChatProcessing()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/messages", new TripDataChatRequest("Find a trip to Seattle"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MissingFoundryConfigurationReturnsGenericRetryableResponse()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, "chat-endpoint-user");

        var response = await client.PostAsJsonAsync("/api/chat/messages", new TripDataChatRequest("Find a trip to Seattle"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("temporarily unavailable", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TripChat:Endpoint", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foundry", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OversizedMessageIsRejectedBeforeChatConfigurationOrRetrieval()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, "chat-endpoint-user");

        var response = await client.PostAsJsonAsync(
            "/api/chat/messages",
            new TripDataChatRequest(new string('q', 2001)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PriorUserTurnCountIsBounded()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, "chat-endpoint-user");

        var response = await client.PostAsJsonAsync(
            "/api/chat/messages",
            new TripDataChatRequest("Follow up", Enumerable.Repeat("previous question", 7).ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}