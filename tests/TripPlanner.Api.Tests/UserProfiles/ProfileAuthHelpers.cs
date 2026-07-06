using TripPlanner.Api.Tests.Infrastructure;

namespace TripPlanner.Api.Tests.UserProfiles;

internal static class ProfileAuthHelpers
{
    public static void AddProfileTestUserHeaders(
        this HttpClient client,
        string userId = TestUsers.UserA,
        string displayName = "Avery Traveler",
        string firstName = "Avery",
        string lastName = "Traveler",
        string email = "avery@example.test")
    {
        client.DefaultRequestHeaders.Remove(TestAuthHandler.TestUserHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.TestNameHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.TestFirstNameHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.TestLastNameHeader);
        client.DefaultRequestHeaders.Remove(TestAuthHandler.TestEmailHeader);

        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestNameHeader, displayName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestFirstNameHeader, firstName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestLastNameHeader, lastName);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestEmailHeader, email);
    }
}
