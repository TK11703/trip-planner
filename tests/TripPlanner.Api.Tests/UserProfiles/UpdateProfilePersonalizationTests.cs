using System.Net.Http.Json;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Api.Tests.UserProfiles;

public class UpdateProfilePersonalizationTests
{
    [Fact]
    public async Task Put_SavesAndClearsPersonalizationPreferences()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();

        var savedResponse = await client.PutAsJsonAsync("/api/profile", new UpdateUserProfileRequest(
            "Avery",
            "Traveler",
            "Avery Traveler",
            "avery@example.test",
            "UTC",
            MapProviders.Bing,
            NotificationPreferences.Default,
            new PersonalizationPreferences("museums", "SEA", "slow travel", "quiet rooms")));
        savedResponse.EnsureSuccessStatusCode();
        var saved = await savedResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("museums", saved!.PersonalizationPreferences.TravelInterests);

        var clearedResponse = await client.PutAsJsonAsync("/api/profile", new UpdateUserProfileRequest(
            "Avery",
            "Traveler",
            "Avery Traveler",
            "avery@example.test",
            "UTC",
            MapProviders.Bing,
            NotificationPreferences.Default,
            new PersonalizationPreferences(" ", null, "", null)));
        clearedResponse.EnsureSuccessStatusCode();
        var cleared = await clearedResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.Null(cleared!.PersonalizationPreferences.TravelInterests);
        Assert.Null(cleared.PersonalizationPreferences.HomeAirport);
        Assert.Null(cleared.PersonalizationPreferences.PreferredTravelStyle);
    }
}
