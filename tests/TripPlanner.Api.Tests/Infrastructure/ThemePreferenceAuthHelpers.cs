using System.Net.Http.Headers;

namespace TripPlanner.Api.Tests.Infrastructure;

public static class ThemePreferenceAuthHelpers
{
    public static HttpRequestMessage WithTestUser(this HttpRequestMessage request, string userId = TestUsers.UserA)
    {
        request.Headers.Add(TestAuthHandler.TestUserHeader, userId);
        request.Headers.Add(TestAuthHandler.TestNameHeader, userId);
        return request;
    }

    public static void AddTestUserHeaders(this HttpClient client, string userId = TestUsers.UserA)
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.TestUserHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.TestNameHeader);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestNameHeader, userId);
    }
}
