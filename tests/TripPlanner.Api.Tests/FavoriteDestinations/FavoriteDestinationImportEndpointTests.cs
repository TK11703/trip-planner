using System.Net;
using System.Net.Http.Json;
using System.Text;
using TripPlanner.Api.Features.FavoriteDestinations;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.FavoriteDestinations;

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
        var jsonResult = await jsonResponse.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        using var csvForm = Form("favorites.csv", "name,address,notes\r\nGarden,Paris,\"early arrival\"\r\n");
        var csvResponse = await client.PostAsync("/api/favorite-destinations/import", csvForm);
        Assert.True(csvResponse.IsSuccessStatusCode, $"CSV import returned {(int)csvResponse.StatusCode}: {await csvResponse.Content.ReadAsStringAsync()}");
        var csvResult = await csvResponse.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();

        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal("Visit", Assert.Single(jsonResult!.Imported).Notes);
        Assert.Equal("early arrival", Assert.Single(csvResult!.Imported).Notes);
        Assert.All(factory.Repository.All, item => Assert.Equal("owner-import", item.Owner));
    }

    [Fact]
    public async Task Import_InvalidRows_ReturnsRowIssuesAndWritesNothing()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        const string json = """[{"name":"Valid","address":"Berlin"},{"name":" ","address":" "}]""";

        using var form = Form("favorites.json", json);
        var response = await client.PostAsync("/api/favorite-destinations/import", form);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Empty(result!.Imported);
        Assert.Contains(result.Errors, error => error.RowNumber == 2 && error.Message.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(factory.Repository.All);
    }

    [Fact]
    public async Task Import_DuplicatesRequireConfirmation_AndConfirmedBatchIsInserted()
    {
        await using var factory = new FavoriteDestinationEndpointsTests.FavoritesApiFactory();
        using var client = factory.CreateClient();
        client.AddUser("owner-import");
        await client.PostAsJsonAsync("/api/favorite-destinations", new CreateFavoriteDestinationRequest("Museum", "Berlin", null));
        const string json = """[{"name":"Museum","address":"Berlin"}]""";

        using var unconfirmedForm = Form("favorites.json", json);
        var unconfirmed = await client.PostAsync("/api/favorite-destinations/import", unconfirmedForm);
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        var warning = await unconfirmed.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();
        using var confirmedForm = Form("favorites.json", json, confirmDuplicates: true);
        var confirmed = await client.PostAsync("/api/favorite-destinations/import", confirmedForm);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        var imported = await confirmed.Content.ReadFromJsonAsync<FavoriteDestinationImportResponse>();

        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        Assert.True(warning!.RequiresDuplicateConfirmation);
        Assert.Single(warning.PossibleDuplicates);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Single(imported!.Imported);
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

    private static MultipartFormDataContent Form(string fileName, string contents, bool confirmDuplicates = false)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(contents, Encoding.UTF8, "application/octet-stream"), "file", fileName);
        if (confirmDuplicates)
        {
            form.Add(new StringContent("true"), "confirmPossibleDuplicates");
        }
        return form;
    }
}