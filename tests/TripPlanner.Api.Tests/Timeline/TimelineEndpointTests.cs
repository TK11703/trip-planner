using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Timeline;
using Xunit;

namespace TripPlanner.Api.Tests.Timeline;

/// <summary>
/// The timeline is the densest read in the app — legs, the items on them, and the unassigned
/// ones, assembled in SQL. Owner scoping therefore has to hold across several joins at once,
/// which is precisely the kind of thing that looks right in a fake and leaks in a query.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(PostgresApiFactory.ApiPostgresCollectionName)]
public class TimelineEndpointTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly PostgresApiFactory _factory;

    public TimelineEndpointTests(PostgresApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestScopeHeader, "access_as_user");
        return client;
    }

    private static string NewUser() => $"user-{Guid.NewGuid():N}";

    /// <summary>A trip with one stay leg, one item on it, and one unassigned item.</summary>
    private async Task<Guid> SeedTripWithTimelineAsync(string ownerUserId, string marker)
    {
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();

        await using var conn = await _factory.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
            VALUES (@tripId, @ownerUserId, @marker, DATE '2026-07-10', DATE '2026-07-20');

            INSERT INTO trip_legs (
                trip_leg_id, trip_id, owner_user_id, title, start_at, end_at,
                start_local, start_time_zone_id, end_local, end_time_zone_id, sort_order, leg_kind)
            VALUES (
                @legId, @tripId, @ownerUserId, @marker || '-leg',
                TIMESTAMPTZ '2026-07-11 08:00+00', TIMESTAMPTZ '2026-07-14 08:00+00',
                TIMESTAMP '2026-07-11 08:00', 'UTC', TIMESTAMP '2026-07-14 08:00', 'UTC', 0, 'stay');

            INSERT INTO tracked_items (
                trip_id, owner_user_id, trip_leg_id, item_type, title,
                starts_at, start_local, start_time_zone_id, display_color, sort_order)
            VALUES
                (@tripId, @ownerUserId, @legId, 'activity', @marker || '-on-leg',
                 TIMESTAMPTZ '2026-07-12 12:00+00', TIMESTAMP '2026-07-12 12:00', 'UTC', 'slate', 0),
                (@tripId, @ownerUserId, NULL, 'activity', @marker || '-unassigned',
                 TIMESTAMPTZ '2026-07-16 12:00+00', TIMESTAMP '2026-07-16 12:00', 'UTC', 'slate', 0);
            """,
            new { tripId, legId, ownerUserId, marker });

        return tripId;
    }

    [Fact]
    public async Task GetTimeline_ReturnsOwnerScopedItems()
    {
        var owner = NewUser();
        var stranger = NewUser();
        var tripId = await SeedTripWithTimelineAsync(owner, "mine");

        // Seeded so the query has another traveler's rows available to leak, if it were going to.
        await SeedTripWithTimelineAsync(stranger, "theirs");

        var response = await ClientFor(owner).GetAsync($"/api/trips/{tripId}/timeline");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var timeline = (await response.Content.ReadFromJsonAsync<TripTimelineResponse>(Web))!;

        Assert.Equal(tripId, timeline.TripId);
        var leg = Assert.Single(timeline.Legs);
        Assert.Equal("mine-leg", leg.Title);
        Assert.Contains(leg.Items, i => i.Title == "mine-on-leg");

        var unassigned = Assert.Single(timeline.UnassignedItems);
        Assert.Equal("mine-unassigned", unassigned.Title);

        // Nothing belonging to the other traveler appears anywhere in the payload.
        var raw = await (await ClientFor(owner).GetAsync($"/api/trips/{tripId}/timeline")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("theirs", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTimeline_ForAnotherTravelersTrip_IsNotFound()
    {
        var owner = NewUser();
        var stranger = NewUser();
        var tripId = await SeedTripWithTimelineAsync(owner, "private");

        var response = await ClientFor(stranger).GetAsync($"/api/trips/{tripId}/timeline");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("private", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
