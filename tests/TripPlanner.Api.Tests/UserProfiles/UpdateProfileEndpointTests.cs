using System.Net;
using System.Net.Http.Json;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Api.Tests.UserProfiles;

public class UpdateProfileEndpointTests
{
    [Fact]
    public async Task Put_UpdatesOnlyCurrentUsersProfile()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders(TestUsers.UserA, "A User", "A", "User", "a@example.test");
        await client.PutAsJsonAsync("/api/profile", new UpdateUserProfileRequest(
            "A",
            "User",
            "A Saved",
            "a.saved@example.test",
            "UTC",
            MapProviders.Bing,
            NotificationPreferences.Default,
            new PersonalizationPreferences(null, null, null, null)));

        client.AddProfileTestUserHeaders(TestUsers.UserB, "B User", "B", "User", "b@example.test");
        var other = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");

        Assert.Equal(TestUsers.UserB, other!.UserId);
        Assert.Equal("B User", other.DisplayName);
        Assert.Equal("b@example.test", other.Email);
    }

    [Fact]
    public async Task Put_RejectsInvalidEmailAndPreservesPreviousValues()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();
        var valid = await client.PutAsJsonAsync("/api/profile", new UpdateUserProfileRequest(
            "Avery",
            "Traveler",
            "Avery Saved",
            "avery.saved@example.test",
            "UTC",
            MapProviders.Bing,
            NotificationPreferences.Default,
            new PersonalizationPreferences(null, null, null, null)));
        valid.EnsureSuccessStatusCode();

        var invalid = await client.PutAsJsonAsync("/api/profile", new UpdateUserProfileRequest(
            "Avery",
            "Traveler",
            "Broken Email",
            "not-an-email",
            "UTC",
            MapProviders.Bing,
            NotificationPreferences.Default,
            new PersonalizationPreferences(null, null, null, null)));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var current = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");
        Assert.Equal("Avery Saved", current!.DisplayName);
        Assert.Equal("avery.saved@example.test", current.Email);
    }
}
