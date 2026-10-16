using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Api.Tests.UserProfiles;

public class UpdateProfilePersonalizationTests
{
    [Fact]
    public async Task Put_IgnoresRetiredFields_AndOnlyReturnsItemColorPersonalization()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();

        var savedResponse = await client.PutAsJsonAsync("/api/profile", new
        {
            FirstName = "Avery",
            LastName = "Traveler",
            DisplayName = "Avery Traveler",
            Email = "avery@example.test",
            TimeZoneId = "UTC",
            MapProvider = MapProviders.Bing,
            NotificationPreferences = NotificationPreferences.Default,
            PersonalizationPreferences = new
            {
                TravelInterests = "museums",
                HomeAirport = "SEA",
                PreferredTravelStyle = "slow travel",
                AccessibilityNotes = "quiet rooms",
                ItemTypeColors = new Dictionary<string, string> { ["activity"] = "teal" }
            }
        });
        savedResponse.EnsureSuccessStatusCode();
        var saved = await savedResponse.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("teal", saved!.PersonalizationPreferences.GetItemColor("activity"));

        foreach (var response in new[] { savedResponse, await client.GetAsync("/api/profile") })
        {
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var personalization = document.RootElement.GetProperty("personalizationPreferences");
            Assert.Equal("itemTypeColors", Assert.Single(personalization.EnumerateObject()).Name);
            Assert.Equal("teal", personalization.GetProperty("itemTypeColors").GetProperty("activity").GetString());
        }
    }
}
