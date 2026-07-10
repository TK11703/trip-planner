using System.Net;
using System.Net.Http.Json;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Api.Tests.UserProfiles;

public class UpdateProfileNotificationTests
{
    [Fact]
    public async Task Put_PersistsNotificationPreferences()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();

        var response = await client.PutAsJsonAsync("/api/profile", new UpdateUserProfileRequest(
            "Avery",
            "Traveler",
            "Avery Traveler",
            "avery@example.test",
            "UTC",
            MapProviders.Bing,
            new NotificationPreferences(new[]
            {
                new NotificationCategoryPreference(NotificationCategories.ItineraryChanges, "Itinerary changes", true, false),
                new NotificationCategoryPreference(NotificationCategories.TripSharing, "Trip sharing", false, false)
            }),
            new PersonalizationPreferences(null, null, null, null)));

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        var itinerary = profile!.NotificationPreferences.Find(NotificationCategories.ItineraryChanges);
        var sharing = profile.NotificationPreferences.Find(NotificationCategories.TripSharing);
        Assert.True(itinerary!.InAppEnabled);
        Assert.False(itinerary.EmailEnabled);
        Assert.False(sharing!.InAppEnabled);
        Assert.False(sharing.EmailEnabled);
    }

    [Fact]
    public async Task Put_RejectsEmailNotificationsWithoutEmail()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders();

        var response = await client.PutAsJsonAsync("/api/profile", new UpdateUserProfileRequest(
            "Avery",
            "Traveler",
            "Avery Traveler",
            null,
            "UTC",
            MapProviders.Bing,
            new NotificationPreferences(new[]
            {
                new NotificationCategoryPreference(NotificationCategories.ItineraryChanges, "Itinerary changes", true, true)
            }),
            new PersonalizationPreferences(null, null, null, null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        Assert.Equal("validation_failed", error!.Code);
        Assert.Equal("Email notifications require a valid email address.", error.Message);
    }
}
