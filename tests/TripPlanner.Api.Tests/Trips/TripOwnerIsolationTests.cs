using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.Trips;

/// <summary>
/// One traveler must never see another's itinerary. The rule is carried in the WHERE clause of
/// every query rather than in a check above them, so it is only meaningfully tested end to end —
/// a real request, a real database, and an assertion about what came back.
///
/// A leak here would not throw or fail a contract. It would quietly render someone else's trip,
/// which is why these assert on absence rather than on an error.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(PostgresApiFactory.ApiPostgresCollectionName)]
public class TripOwnerIsolationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly PostgresApiFactory _factory;

    public TripOwnerIsolationTests(PostgresApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestScopeHeader, "access_as_user");
        return client;
    }

    private static string NewUser() => $"user-{Guid.NewGuid():N}";

    private async Task<Guid> SeedTripAsync(string ownerUserId, string name)
    {
        var tripId = Guid.NewGuid();
        await using var conn = await _factory.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
            VALUES (@tripId, @ownerUserId, @name, DATE '2026-07-10', DATE '2026-07-20');
            """,
            new { tripId, ownerUserId, name });
        return tripId;
    }

    [Fact]
    public async Task UserB_CannotReadUserATripById()
    {
        var userA = NewUser();
        var userB = NewUser();
        var tripId = await SeedTripAsync(userA, "A's trip");

        var mine = await ClientFor(userA).GetAsync($"/api/trips/{tripId}");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        var theirs = await ClientFor(userB).GetAsync($"/api/trips/{tripId}");

        // Not forbidden — not found. The answer must not confirm the trip exists.
        Assert.Equal(HttpStatusCode.NotFound, theirs.StatusCode);
        var body = await theirs.Content.ReadAsStringAsync();
        Assert.DoesNotContain("A's trip", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecentTrips_OnlyReturnsCurrentUserTrips()
    {
        var userA = NewUser();
        var userB = NewUser();
        await SeedTripAsync(userA, "A one");
        await SeedTripAsync(userA, "A two");
        await SeedTripAsync(userB, "B only");

        var response = await ClientFor(userA).GetAsync("/api/trips?page=1&pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = (await response.Content.ReadFromJsonAsync<TripListResponse>(Web))!;
        var names = list.Trips.Select(t => t.Name).ToArray();

        Assert.Contains("A one", names);
        Assert.Contains("A two", names);
        Assert.DoesNotContain("B only", names);
    }

    /// <summary>
    /// Writes are scoped the same way reads are: a trip that is not yours cannot be edited, and
    /// the refusal leaves the owner's copy untouched.
    /// </summary>
    [Fact]
    public async Task UserB_CannotUpdateUserATrip()
    {
        var userA = NewUser();
        var userB = NewUser();
        var tripId = await SeedTripAsync(userA, "Original");

        var response = await ClientFor(userB).PutAsJsonAsync(
            $"/api/trips/{tripId}",
            new UpdateTripRequest("Hijacked", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var conn = await _factory.OpenAsync();
        var name = await conn.ExecuteScalarAsync<string>(
            "SELECT name FROM trips WHERE trip_id = @tripId", new { tripId });
        Assert.Equal("Original", name);
    }
}
