using System.Net;
using System.Net.Http.Json;
using System.Text;
using TripPlanner.Api.Features.FavoriteDestinations;
using TripPlanner.Api.Features.Places;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.FavoriteDestinations;
using TripPlanner.Contracts.Places;

namespace TripPlanner.Api.Tests.FavoriteDestinations;

public sealed class FavoriteDestinationImportEndpointTests
{
    [Fact]
    public async Task Import_RequiresAuthentication()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();

        using var form = Form("favorites.json", "[]");
        var response = await client.PostAsync("/api/favorite-destinations/import", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Import_AcceptsJsonAndCsv_AndDerivesOwnerFromAuthenticatedUser()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");

        using var jsonForm = Form("favorites.json", """[{"name":"Museum","address":"Berlin","notes":"Visit"}]""");
        var jsonResponse = await client.PostAsync("/api/favorite-destinations/import", jsonForm);
        Assert.True(jsonResponse.IsSuccessStatusCode, $"JSON import returned {(int)jsonResponse.StatusCode}: {await jsonResponse.Content.ReadAsStringAsync()}");
        var jsonQueued = await jsonResponse.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var jsonResult = await WaitForImportAsync(client, jsonQueued!.Import!.ImportId);
        using var csvForm = Form("favorites.csv", "name,address,notes\r\nGarden,Paris,\"early arrival\"\r\n");
        var csvResponse = await client.PostAsync("/api/favorite-destinations/import", csvForm);
        Assert.True(csvResponse.IsSuccessStatusCode, $"CSV import returned {(int)csvResponse.StatusCode}: {await csvResponse.Content.ReadAsStringAsync()}");
        var csvQueued = await csvResponse.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var csvResult = await WaitForImportAsync(client, csvQueued!.Import!.ImportId);

        Assert.Equal(HttpStatusCode.Accepted, jsonResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, csvResponse.StatusCode);
        Assert.Equal((FavoriteDestinationImportStatus.Completed, 1), (jsonResult.Status, jsonResult.ImportedCount));
        Assert.Equal((FavoriteDestinationImportStatus.Completed, 1), (csvResult.Status, csvResult.ImportedCount));
        Assert.Contains(factory.Repository.All, item => item.Favorite.Notes == "Visit");
        Assert.Contains(factory.Repository.All, item => item.Favorite.Notes == "early arrival");
        Assert.All(factory.Repository.All, item => Assert.Equal("owner-import", item.Owner));
    }

    [Fact]
    public async Task Import_InvalidRows_ReturnsRowIssuesAndQueuesNothing()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        const string json = """[{"name":"Valid","address":"Berlin"},{"name":" ","address":" "}]""";

        using var form = Form("favorites.json", json);
        var response = await client.PostAsync("/api/favorite-destinations/import", form);
        var result = await response.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Null(result!.Import);
        Assert.Contains(result.Errors, error => error.RowNumber == 2 && error.Message.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await client.GetFromJsonAsync<FavoriteDestinationImportJobDto[]>("/api/favorite-destinations/imports") ?? []);
        Assert.Empty(factory.Repository.All);
    }

    [Fact]
    public async Task Import_DuplicatesWaitForReview_AndConfirmedBatchIsInserted()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        await client.PostAsJsonAsync("/api/favorite-destinations", new CreateFavoriteDestinationRequest("Museum", "Berlin", null));
        const string json = """[{"name":"Museum","address":"Berlin"}]""";

        using var form = Form("favorites.json", json);
        var queued = await (await client.PostAsync("/api/favorite-destinations/import", form))
            .Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var review = await WaitForImportAsync(client, queued!.Import!.ImportId);
        var unconfirmed = await Complete(client, review.ImportId, new CompleteFavoriteDestinationImportRequest([]));
        var confirmed = await Complete(client, review.ImportId, new CompleteFavoriteDestinationImportRequest([], ConfirmPossibleDuplicates: true));
        var completed = await confirmed.Content.ReadFromJsonAsync<FavoriteDestinationImportJobDto>();

        Assert.Equal(FavoriteDestinationImportStatus.NeedsReview, review.Status);
        Assert.Equal(1, Assert.Single(review.PossibleDuplicates).RowNumber);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal((FavoriteDestinationImportStatus.Completed, 1), (completed!.Status, completed.ImportedCount));
        Assert.Equal(2, factory.Repository.All.Count);
    }

    [Fact]
    public async Task Import_SameNamePlaces_WaitForSelection_AndImportChosenLocation()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        factory.Lookup.MatchesByQuery["Blue Bottle Coffee"] =
        [
            new PlaceMatch("Blue Bottle Coffee", new PlaceAddressComponents("Oakland", "United States", 37.8, -122.27, "300 Webster St, Oakland, CA 94607")),
            new PlaceMatch("Blue Bottle Coffee", new PlaceAddressComponents("San Francisco", "United States", 37.78, -122.41, "66 Mint St, San Francisco, CA 94103")),
            new PlaceMatch("Blue Bottle Café", new PlaceAddressComponents("Tokyo", "Japan", 35.68, 139.8, "Tokyo, Japan"))
        ];
        const string csv = """
        Title,Note,URL,Tags,Comment
        Blue Bottle Coffee,Pour-over,https://www.google.com/maps/place/Blue+Bottle+Coffee/data=!4m2!3m1!1s0x0:0x1,,
        """;

        using var form = Form("saved.csv", csv);
        var queued = await (await client.PostAsync("/api/favorite-destinations/import", form))
            .Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var review = await WaitForImportAsync(client, queued!.Import!.ImportId);
        var missingSelection = await Complete(client, review.ImportId, new CompleteFavoriteDestinationImportRequest([]));
        var outOfRange = await Complete(client, review.ImportId, new CompleteFavoriteDestinationImportRequest([new FavoriteDestinationImportSelection(2, 5)]));
        var chosen = await Complete(client, review.ImportId, new CompleteFavoriteDestinationImportRequest([new FavoriteDestinationImportSelection(2, 1)]));

        Assert.Equal(FavoriteDestinationImportStatus.NeedsReview, review.Status);
        var ambiguity = Assert.Single(review.Ambiguities);
        Assert.Equal((2, "Blue Bottle Coffee"), (ambiguity.RowNumber, ambiguity.Name));
        Assert.Equal(["300 Webster St, Oakland, CA 94607", "66 Mint St, San Francisco, CA 94103"], ambiguity.Candidates.Select(candidate => candidate.Address));
        Assert.Equal((HttpStatusCode)422, missingSelection.StatusCode);
        Assert.Equal((HttpStatusCode)422, outOfRange.StatusCode);
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        var favorite = Assert.Single(factory.Repository.All).Favorite;
        Assert.Equal(("Blue Bottle Coffee", "66 Mint St, San Francisco, CA 94103", "San Francisco", 37.78, -122.41, "Pour-over"),
            (favorite.Name, favorite.Address, favorite.City, favorite.Latitude, favorite.Longitude, favorite.Notes));
    }

    [Fact]
    public async Task Import_NoneOfTheCandidates_KeepsSubmittedAddressWithoutLocation()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        factory.Lookup.MatchesByQuery["Paris"] =
        [
            new PlaceMatch("Paris", new PlaceAddressComponents("Paris", "France", 48.85, 2.35, "Paris, France")),
            new PlaceMatch("Paris", new PlaceAddressComponents("Paris", "United States", 33.66, -95.55, "Paris, TX"))
        ];

        using var form = Form("favorites.json", """[{"name":"Trip idea","address":"Paris"}]""");
        var queued = await (await client.PostAsync("/api/favorite-destinations/import", form))
            .Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var review = await WaitForImportAsync(client, queued!.Import!.ImportId);
        var response = await Complete(client, review.ImportId, new CompleteFavoriteDestinationImportRequest([new FavoriteDestinationImportSelection(1, null)]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var favorite = Assert.Single(factory.Repository.All).Favorite;
        Assert.Equal(("Paris", null, null), (favorite.Address, favorite.Country, favorite.Latitude));
    }

    [Fact]
    public async Task Import_IsPrivateToItsOwner_AndCanBeDiscarded()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var owner = factory.CreateClient();
        using var other = factory.CreateClient();
        owner.AddUser("owner-import");
        other.AddUser("someone-else");
        factory.Lookup.MatchesByQuery["Starbucks"] =
        [
            new PlaceMatch("Starbucks", new PlaceAddressComponents("Seattle", "United States", 47.61, -122.34, "1912 Pike Pl, Seattle, WA 98101")),
            new PlaceMatch("Starbucks", new PlaceAddressComponents("Seattle", "United States", 47.6, -122.33, "1124 Pike St, Seattle, WA 98101"))
        ];

        using var form = Form("favorites.json", """[{"name":"Coffee","address":"Starbucks"}]""");
        var queued = await (await owner.PostAsync("/api/favorite-destinations/import", form))
            .Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var importId = queued!.Import!.ImportId;
        await WaitForImportAsync(owner, importId);

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/favorite-destinations/imports/{importId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Complete(other, importId, new CompleteFavoriteDestinationImportRequest([new FavoriteDestinationImportSelection(1, 0)]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/favorite-destinations/imports/{importId}")).StatusCode);
        Assert.Empty(await other.GetFromJsonAsync<FavoriteDestinationImportJobDto[]>("/api/favorite-destinations/imports") ?? []);
        Assert.Single(await owner.GetFromJsonAsync<FavoriteDestinationImportJobDto[]>("/api/favorite-destinations/imports") ?? []);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/favorite-destinations/imports/{importId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/favorite-destinations/imports/{importId}")).StatusCode);
        Assert.Empty(factory.Repository.All);
    }

    [Fact]
    public async Task Import_RejectsUploadsAboveLimit()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        using var form = Form("large.json", new string(' ', ImportFavoriteDestinationsEndpoint.MaximumUploadBytes + 1));

        var response = await client.PostAsync("/api/favorite-destinations/import", form);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(factory.Repository.All);
    }

    [Fact]
    public void FindSameNameCandidates_OnlyFlagsDistinctPlacesNamedLikeTheSearch()
    {
        PlaceMatch Match(string? name, string address, double latitude)
            => new(name, new PlaceAddressComponents(null, null, latitude, 2.0, address));

        var ambiguous = FavoriteDestinationImportProcessor.FindSameNameCandidates(
            [Match(null, "Starbucks Way, Minden, NV", 39.0), Match("Starbucks", "111 Elizabeth St, Toronto", 43.6), Match("starbucks ", "99 Bank St, Ottawa", 45.4), Match("Starbucks Reserve", "Seattle", 47.6)],
            "Starbucks");
        var sameAddress = FavoriteDestinationImportProcessor.FindSameNameCandidates(
            [Match("Louvre", "Rue de Rivoli, Paris", 48.86), Match("Louvre", "rue de rivoli, paris", 48.861)],
            "Louvre");
        var notAPlace = FavoriteDestinationImportProcessor.FindSameNameCandidates(
            [Match(null, "Main St", 40.0), Match(null, "Main Street", 41.0)],
            "Main St");

        Assert.Equal(["111 Elizabeth St, Toronto", "99 Bank St, Ottawa"], ambiguous.Select(candidate => candidate.Address));
        Assert.Single(sameAddress);
        Assert.Empty(notAPlace);
    }

    [Fact]
    public async Task Import_SingleExactNameMatch_WinsOverTopFuzzyResult()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        factory.Lookup.MatchesByQuery["Galeries Lafayette Haussmann"] =
        [
            new PlaceMatch("Galeries Lafayette Rooftop", new PlaceAddressComponents("Paris", "France", 48.87, 2.33, "42 Boulevard Haussmann, 75009 Paris")),
            new PlaceMatch("Galeries Lafayette Haussmann", new PlaceAddressComponents("Paris", "France", 48.8738, 2.332, "40 Boulevard Haussmann, 75009 Paris"))
        ];
        const string csv = """
        Title,Note,URL,Tags,Comment
        Galeries Lafayette Haussmann,,https://www.google.com/maps/place/Galeries+Lafayette+Haussmann/data=!4m2!3m1!1s0x0:0x1,,
        """;

        using var form = Form("saved.csv", csv);
        var queued = await (await client.PostAsync("/api/favorite-destinations/import", form))
            .Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        var completed = await WaitForImportAsync(client, queued!.Import!.ImportId);

        Assert.Equal(FavoriteDestinationImportStatus.Completed, completed.Status);
        Assert.Equal("40 Boulevard Haussmann, 75009 Paris", Assert.Single(factory.Repository.All).Favorite.Address);
    }

    internal static async Task<FavoriteDestinationImportJobDto> WaitForImportAsync(HttpClient client, Guid importId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (true)
        {
            var job = await client.GetFromJsonAsync<FavoriteDestinationImportJobDto>($"/api/favorite-destinations/imports/{importId}");
            if (job!.Status is not (FavoriteDestinationImportStatus.Queued or FavoriteDestinationImportStatus.Processing))
            {
                return job;
            }
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Import {importId} is still {job.Status}.");
            }
            await Task.Delay(50);
        }
    }

    private static Task<HttpResponseMessage> Complete(HttpClient client, Guid importId, CompleteFavoriteDestinationImportRequest request)
        => client.PostAsJsonAsync($"/api/favorite-destinations/imports/{importId}/complete", request);

    private static MultipartFormDataContent Form(string fileName, string contents)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(contents, Encoding.UTF8, "application/octet-stream"), "file", fileName);
        return form;
    }
}