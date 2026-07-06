using System.Net;
using System.Net.Http.Json;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Profile;

namespace TripPlanner.Api.Tests.UserProfiles;

public class GetProfileEndpointTests
{
    [Fact]
    public async Task Get_CreatesProfileFromAuthenticatedClaims()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders(displayName: "Avery Azure", firstName: "Avery", lastName: "Azure", email: "avery.azure@example.test");

        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");

        Assert.NotNull(profile);
        Assert.Equal(TestUsers.UserA, profile!.UserId);
        Assert.Equal("Avery", profile.FirstName);
        Assert.Equal("Azure", profile.LastName);
        Assert.Equal("Avery Azure", profile.DisplayName);
        Assert.Equal("avery.azure@example.test", profile.Email);
        Assert.True(profile.IsComplete);
    }

    [Fact]
    public async Task Get_DoesNotOverwriteExistingSavedValuesWithChangedClaims()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();
        client.AddProfileTestUserHeaders(displayName: "Original Name", firstName: "Original", lastName: "Name", email: "original@example.test");
        _ = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");

        client.AddProfileTestUserHeaders(displayName: "Changed Name", firstName: "Changed", lastName: "Name", email: "changed@example.test");
        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/profile");

        Assert.Equal("Original", profile!.FirstName);
        Assert.Equal("Original Name", profile.DisplayName);
        Assert.Equal("original@example.test", profile.Email);
    }

    [Fact]
    public async Task Get_AnonymousRequestIsUnauthorized()
    {
        await using var factory = new ProfileApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
