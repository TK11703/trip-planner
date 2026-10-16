using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Api.Tests.UserProfiles;

public class ItemTypeColorPreferenceTests
{
    private static UpdateUserProfileRequest Request(IReadOnlyDictionary<string, string>? colors) =>
        new("Avery", "Traveler", "Avery", "avery@example.test", "UTC", MapProviders.Bing,
            NotificationPreferences.Default, new(colors));

    [Fact]
    public async Task Preferences_RoundTrip_AreUserSpecific_AndCanBeCleared()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();
        var response = await client.PutAsJsonAsync("/api/profile", Request(new Dictionary<string, string>
        {
            ["Activity"] = "PURPLE", ["event"] = "teal"
        }));
        response.EnsureSuccessStatusCode();
        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");
        Assert.Equal("purple", profile!.PersonalizationPreferences.GetItemColor("activity"));
        Assert.Equal("teal", profile.PersonalizationPreferences.GetItemColor("event"));
        Assert.Equal("slate", profile.PersonalizationPreferences.GetItemColor("reservation"));

        client.AddProfileTestUserHeaders(userId: "another-user");
        var other = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");
        Assert.Equal("slate", other!.PersonalizationPreferences.GetItemColor("activity"));

        client.AddProfileTestUserHeaders();
        (await client.PutAsJsonAsync("/api/profile", Request(new Dictionary<string, string>()))).EnsureSuccessStatusCode();
        var cleared = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");
        Assert.Empty(cleared!.PersonalizationPreferences.ItemTypeColors!);
    }

    [Theory]
    [InlineData("unknown", "blue")]
    [InlineData("activity", "unknown")]
    [InlineData("activity", "")]
    [InlineData("activity", null)]
    public async Task InvalidPreference_IsRejected(string type, string? color)
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();
        var response = await client.PutAsJsonAsync("/api/profile", Request(new Dictionary<string, string>
        {
            [type] = color!
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateTypeIgnoringCase_IsRejected()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();
        var response = await client.PutAsJsonAsync("/api/profile", Request(new Dictionary<string, string>
        {
            ["activity"] = "blue", ["Activity"] = "teal"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUpdate_IsUnauthorized()
    {
        await using var factory = new ProfileApiFactory();
        var response = await factory.CreateClient().PutAsJsonAsync("/api/profile",
            Request(new Dictionary<string, string> { ["activity"] = "blue" }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
