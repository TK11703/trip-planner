using System.Net.Http.Json;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Profile;
using Xunit;

namespace TripPlanner.Api.Tests.UserProfiles;

public class UserProfileMapProviderTests
{
    private static UpdateUserProfileRequest Request(string mapProvider)
        => new(
            "Avery",
            "Traveler",
            "Avery Traveler",
            "avery@example.test",
            "UTC",
            mapProvider,
            NotificationPreferences.Default,
            new PersonalizationPreferences(null, null, null, null));

    [Fact]
    public async Task NewProfile_DefaultsToBing()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();

        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");

        Assert.Equal(MapProviders.Bing, profile!.MapProvider);
    }

    [Fact]
    public async Task Put_Google_RoundTrips()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();

        var response = await client.PutAsJsonAsync("/api/profile", Request(MapProviders.Google));
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.Equal(MapProviders.Google, saved!.MapProvider);

        var reloaded = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");
        Assert.Equal(MapProviders.Google, reloaded!.MapProvider);
    }

    [Fact]
    public async Task Put_UnknownValue_StoresBing()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();

        var response = await client.PutAsJsonAsync("/api/profile", Request("Nonsense"));
        response.EnsureSuccessStatusCode();
        var saved = await response.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.Equal(MapProviders.Bing, saved!.MapProvider);
    }
}
