using System.Net;
using System.Net.Http.Json;
using Dapper;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.TripDataChat;

namespace TripPlanner.Api.Tests.TripDataChat;

[Collection(PostgresApiFactory.ApiPostgresCollectionName)]
public sealed class TripDataChatSecurityTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task ChatRequestCannotMutateTripsLegsItemsOrShares()
    {
        var ownerId = $"chat-security-{Guid.NewGuid():N}";
        var memberId = $"chat-security-member-{Guid.NewGuid():N}";
        var tripId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await using var connection = await factory.OpenAsync();
        await connection.ExecuteAsync(
            "INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date) VALUES (@tripId, @ownerId, 'Read-only trip', DATE '2026-01-01', DATE '2026-01-02')",
            new { tripId, ownerId });
        await connection.ExecuteAsync(
            "INSERT INTO trip_legs (trip_leg_id, trip_id, owner_user_id, title, start_at, end_at, start_local, start_time_zone_id, end_local, end_time_zone_id, leg_kind, sort_order) VALUES (@legId, @tripId, @ownerId, 'Stay', TIMESTAMPTZ '2026-01-01 10:00+00', TIMESTAMPTZ '2026-01-02 10:00+00', TIMESTAMP '2026-01-01 10:00', 'UTC', TIMESTAMP '2026-01-02 10:00', 'UTC', 'stay', 0)",
            new { legId, tripId, ownerId });
        await connection.ExecuteAsync(
            "INSERT INTO tracked_items (tracked_item_id, trip_id, owner_user_id, item_type, title, starts_at, start_local, start_time_zone_id, display_color, sort_order) VALUES (@itemId, @tripId, @ownerId, 'activity', 'Museum', TIMESTAMPTZ '2026-01-01 12:00+00', TIMESTAMP '2026-01-01 12:00', 'UTC', 'slate', 0)",
            new { itemId, tripId, ownerId });
        await connection.ExecuteAsync(
            "INSERT INTO trip_shares (trip_id, member_user_id, access_level, created_by_user_id) VALUES (@tripId, @memberId, 'viewer', @ownerId)",
            new { tripId, memberId, ownerId });
        var before = await CountsAsync(connection, tripId);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, ownerId);
        var payload = new
        {
            message = "Summarize this trip",
            tripId,
            ownerUserId = ownerId,
            writeOperation = new { action = "delete", sourceId = legId }
        };
        var response = await client.PostAsJsonAsync("/api/chat/messages", payload);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(before, await CountsAsync(connection, tripId));
    }

    private static Task<(int Trips, int Legs, int Items, int Shares)> CountsAsync(Npgsql.NpgsqlConnection connection, Guid tripId) =>
        connection.QuerySingleAsync<(int Trips, int Legs, int Items, int Shares)>(
            "SELECT (SELECT count(*) FROM trips WHERE trip_id = @tripId) AS \"Trips\", (SELECT count(*) FROM trip_legs WHERE trip_id = @tripId) AS \"Legs\", (SELECT count(*) FROM tracked_items WHERE trip_id = @tripId) AS \"Items\", (SELECT count(*) FROM trip_shares WHERE trip_id = @tripId) AS \"Shares\"",
            new { tripId });
}