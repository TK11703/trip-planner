using System.Net;
using System.Net.Http.Json;
using TripPlanner.Api.Features.FavoriteDestinations;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;

namespace TripPlanner.Api.Tests.FavoriteDestinations;

public sealed class FavoriteDestinationMaintenanceTests
{
    [Fact]
    public async Task List_SearchesAllResearchFieldsAndReturnsStableCountryCityNameOrder()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-a");

        await CreateAsync(factory, client, "Zurich place", "Zurich address", "Zurich", "Switzerland", "lake notes");
        await CreateAsync(factory, client, "Munich place", "Munich address", "Munich", "Germany", "museum notes");
        await CreateAsync(factory, client, "Berlin place", "Berlin address", "Berlin", "Germany", "history notes");
        await CreateAsync(factory, client, "France place", "Paris address", "Paris", "France", "river notes");
        await CreateAsync(factory, client, "Unknown place", "Free-form address", null, null, "unique hidden note");

        var all = await client.GetFromJsonAsync<FavoriteDestinationDto[]>("/api/favorite-destinations");
        var notesMatches = await client.GetFromJsonAsync<FavoriteDestinationDto[]>("/api/favorite-destinations?q=UNIQUE%20HIDDEN");

        Assert.Equal(["France place", "Berlin place", "Munich place", "Zurich place", "Unknown place"], all!.Select(item => item.Name));
        Assert.Single(notesMatches!);
        Assert.Equal("Unknown place", notesMatches[0].Name);
    }

    [Fact]
    public async Task Update_ValidatesFields_AndHidesOtherOwnersFavorites()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var ownerClient = factory.CreateClient();
        using var otherClient = factory.CreateClient();
        ownerClient.AddUser("owner-a");
        otherClient.AddUser("owner-b");
        var createdResponse = await CreateAsync(factory, ownerClient, "Museum", "Museum address", "Paris", "France", "Before");
        var created = await createdResponse.Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        var changed = new UpdateFavoriteDestinationRequest("Museum", "Museum address", "After");

        var invalid = await ownerClient.PutAsJsonAsync($"/api/favorite-destinations/{created!.FavoriteDestinationId}", changed with { Address = " " });
        var foreign = await otherClient.PutAsJsonAsync($"/api/favorite-destinations/{created.FavoriteDestinationId}", changed);
        var updated = await ownerClient.PutAsJsonAsync($"/api/favorite-destinations/{created.FavoriteDestinationId}", changed);
        var response = await updated.Content.ReadFromJsonAsync<FavoriteDestinationDto>();

        Assert.Equal((HttpStatusCode)422, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("After", response!.Notes);
        Assert.Equal("owner-a", factory.Repository.OwnerOf(created.FavoriteDestinationId));
    }

    [Fact]
    public async Task Delete_UsesIndistinguishableNotFoundForMissingAndOtherOwner()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var ownerClient = factory.CreateClient();
        using var otherClient = factory.CreateClient();
        ownerClient.AddUser("owner-a");
        otherClient.AddUser("owner-b");
        var createdResponse = await CreateAsync(factory, ownerClient, "Museum", "Museum address", "Paris", "France", null);
        var created = await createdResponse.Content.ReadFromJsonAsync<FavoriteDestinationDto>();

        var foreign = await otherClient.DeleteAsync($"/api/favorite-destinations/{created!.FavoriteDestinationId}");
        var deleted = await ownerClient.DeleteAsync($"/api/favorite-destinations/{created.FavoriteDestinationId}");
        var missing = await ownerClient.DeleteAsync($"/api/favorite-destinations/{created.FavoriteDestinationId}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task BulkDelete_RemovesOnlyCallersFavorites_AndValidatesIds()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var ownerClient = factory.CreateClient();
        using var otherClient = factory.CreateClient();
        ownerClient.AddUser("owner-a");
        otherClient.AddUser("owner-b");
        var first = await (await CreateAsync(factory, ownerClient, "First", "First address", null, null, null)).Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        var second = await (await CreateAsync(factory, ownerClient, "Second", "Second address", null, null, null)).Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        var kept = await (await CreateAsync(factory, ownerClient, "Kept", "Kept address", null, null, null)).Content.ReadFromJsonAsync<FavoriteDestinationDto>();
        var ids = new[] { first!.FavoriteDestinationId, second!.FavoriteDestinationId };

        var foreign = await otherClient.PostAsJsonAsync("/api/favorite-destinations/delete", new DeleteFavoriteDestinationsRequest(ids));
        var foreignResult = await foreign.Content.ReadFromJsonAsync<DeleteFavoriteDestinationsResponse>();
        var empty = await ownerClient.PostAsJsonAsync("/api/favorite-destinations/delete", new DeleteFavoriteDestinationsRequest([]));
        var tooMany = await ownerClient.PostAsJsonAsync("/api/favorite-destinations/delete", new DeleteFavoriteDestinationsRequest(
            Enumerable.Range(0, DeleteFavoriteDestinationsEndpoint.MaximumIds + 1).Select(_ => Guid.NewGuid()).ToArray()));
        var deleted = await ownerClient.PostAsJsonAsync("/api/favorite-destinations/delete", new DeleteFavoriteDestinationsRequest([.. ids, .. ids]));
        var deletedResult = await deleted.Content.ReadFromJsonAsync<DeleteFavoriteDestinationsResponse>();

        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        Assert.Equal(0, foreignResult!.DeletedCount);
        Assert.Equal((HttpStatusCode)422, empty.StatusCode);
        Assert.Equal((HttpStatusCode)422, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(2, deletedResult!.DeletedCount);
        Assert.Equal(kept!.FavoriteDestinationId, Assert.Single(factory.Repository.All).Favorite.FavoriteDestinationId);
    }

    private static Task<HttpResponseMessage> CreateAsync(
        FavoriteDestinationEndpointsTests.FavoritesApiFactory factory,
        HttpClient client,
        string name,
        string address,
        string? city,
        string? country,
        string? notes)
    {
        if (city is not null || country is not null)
        {
            factory.Lookup.ResolutionsByAddress[address] = new PlaceAddressComponents(city, country, null, null);
        }
        return client.PostAsJsonAsync("/api/favorite-destinations", new CreateFavoriteDestinationRequest(
            name, address, notes));
    }
}