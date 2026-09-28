using Dapper;
using Npgsql;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using TripPlanner.Database.TripDataChat;

namespace TripPlanner.Database.Tests.TripDataChat;

[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public sealed class TripSearchDocumentsRepositoryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task CandidateQueryFiltersOwnerAndCurrentSharesBeforeVectorRanking()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        const string callerId = "chat-query-caller";
        const string callerEmail = "chat-invite@example.test";
        var ownerTrip = Guid.NewGuid();
        var viewerTrip = Guid.NewGuid();
        var collaboratorTrip = Guid.NewGuid();
        var inviteTrip = Guid.NewGuid();
        var inaccessibleTrip = Guid.NewGuid();
        var trips = new[]
        {
            (ownerTrip, callerId, "owner"),
            (viewerTrip, "other-viewer-owner", "viewer-owner"),
            (collaboratorTrip, "other-collaborator-owner", "collaborator-owner"),
            (inviteTrip, "other-invite-owner", "invite-owner"),
            (inaccessibleTrip, "inaccessible-owner", "inaccessible-owner"),
        };

        foreach (var (tripId, ownerId, prefix) in trips)
        {
            await connection.ExecuteAsync(
                "INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date) VALUES (@tripId, @ownerId, @prefix, DATE '2026-01-01', DATE '2026-01-02')",
                new { tripId, ownerId, prefix });
        }

        await connection.ExecuteAsync(
            "INSERT INTO trip_shares (trip_id, member_user_id, member_email, access_level, created_by_user_id) VALUES (@tripId, @memberId, @email, @access, @ownerId)",
            new { tripId = viewerTrip, memberId = callerId, email = "viewer@example.test", access = "viewer", ownerId = "other-viewer-owner" });
        await connection.ExecuteAsync(
            "INSERT INTO trip_shares (trip_id, member_user_id, member_email, access_level, created_by_user_id) VALUES (@tripId, @memberId, @email, @access, @ownerId)",
            new { tripId = collaboratorTrip, memberId = callerId, email = "collaborator@example.test", access = "collaborator", ownerId = "other-collaborator-owner" });
        await connection.ExecuteAsync(
            "INSERT INTO trip_shares (trip_id, member_user_id, member_email, access_level, created_by_user_id) VALUES (@tripId, @memberId, @email, @access, @ownerId)",
            new { tripId = inviteTrip, memberId = "pending-invite-user", email = callerEmail.ToUpperInvariant(), access = "viewer", ownerId = "other-invite-owner" });

        foreach (var (tripId, ownerId, _) in trips)
        {
            var embedding = tripId == inaccessibleTrip ? "[1,0,0]" : "[0,1,0]";
            await connection.ExecuteAsync(
                "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) VALUES (@tripId, @ownerId, 'trip', @sourceId, 'hash', now(), CAST(@embedding AS vector), 'test', 3, now())",
                new { tripId, ownerId, sourceId = tripId, embedding });
        }

        var sql = new SqlFileProvider().Get("Queries/TripDataChat/FindAuthorizedCandidates.sql");
        var parameters = new
        {
            CallerUserId = callerId,
            CallerEmail = callerEmail,
            Embedding = "[1,0,0]",
            EmbeddingDimensions = 3,
            TopK = 10
        };
        var all = (await connection.QueryAsync<TripSearchCandidate>(sql, parameters)).ToArray();

        Assert.Equal(4, all.Length);
        Assert.Contains(all, candidate => candidate.TripId == ownerTrip);
        Assert.Contains(all, candidate => candidate.TripId == viewerTrip);
        Assert.Contains(all, candidate => candidate.TripId == collaboratorTrip);
        Assert.Contains(all, candidate => candidate.TripId == inviteTrip);
        Assert.DoesNotContain(all, candidate => candidate.TripId == inaccessibleTrip);
        Assert.All(all, candidate => Assert.Equal("trip", candidate.SourceKind));

        var nearest = (await connection.QueryAsync<TripSearchCandidate>(sql, new { parameters.CallerUserId, parameters.CallerEmail, parameters.Embedding, parameters.EmbeddingDimensions, TopK = 1 })).ToArray();
        Assert.Single(nearest);
        Assert.NotEqual(inaccessibleTrip, nearest[0].TripId);

        var repository = new TripSearchDocumentRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());
        Assert.True(await repository.HasAccessibleTripsAsync(callerId, callerEmail, default));
        Assert.False(await repository.HasAccessibleTripsAsync("inaccessible-query-user", null, default));
        var requestCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM unnest(@TripIds::uuid[], @SourceKinds::text[], @SourceIds::uuid[]) AS r(trip_id, source_kind, source_id) INNER JOIN trips t ON t.trip_id = r.trip_id AND r.source_kind = 'trip' AND r.source_id = t.trip_id",
            new { TripIds = all.Select(c => c.TripId).ToArray(), SourceKinds = all.Select(c => c.SourceKind).ToArray(), SourceIds = all.Select(c => c.SourceId).ToArray() });
        Assert.Equal(4, requestCount);
        var canonicalSources = await repository.GetAuthorizedSourcesAsync(callerId, callerEmail, all, default);
        Assert.Equal(4, canonicalSources.Count);
        Assert.Contains(canonicalSources, source => source.TripId == viewerTrip && source.TripName == "viewer-owner");

        var legId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO trip_legs (trip_leg_id, trip_id, owner_user_id, title, start_at, start_local, start_time_zone_id, end_local, end_time_zone_id, leg_kind) VALUES (@legId, @tripId, @ownerId, 'Current stay', TIMESTAMPTZ '2026-01-01 12:00+00', TIMESTAMP '2026-01-01 12:00', 'UTC', TIMESTAMP '2026-01-02 12:00', 'UTC', 'stay')",
            new { legId, tripId = ownerTrip, ownerId = callerId });
        await connection.ExecuteAsync(
            "INSERT INTO tracked_items (tracked_item_id, trip_id, owner_user_id, trip_leg_id, item_type, title, starts_at, start_local, start_time_zone_id, confirmation_code, notes, sort_order) VALUES (@itemId, @tripId, @ownerId, @legId, 'reservation', 'Current booking', TIMESTAMPTZ '2026-01-01 13:00+00', TIMESTAMP '2026-01-01 13:00', 'UTC', 'SECRET-CODE-12345', 'Visible note', 0)",
            new { itemId, tripId = ownerTrip, ownerId = callerId, legId });
        var itinerarySources = await repository.GetAuthorizedSourcesAsync(callerId, callerEmail,
        [
            new TripSearchCandidate(ownerTrip, "leg", legId, 0),
            new TripSearchCandidate(ownerTrip, "tracked_item", itemId, 0)
        ], default);
        Assert.Contains(itinerarySources, source => source.SourceKind == "leg" && source.SourceLabel == "Current stay");
        var itemSource = Assert.Single(itinerarySources, source => source.SourceKind == "tracked_item");
        Assert.Equal("Current booking", itemSource.SourceLabel);
        Assert.DoesNotContain("SECRET-CODE-12345", itemSource.SearchText, StringComparison.Ordinal);

        var mismatchedOwnerLegId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO trip_legs (trip_leg_id, trip_id, owner_user_id, title, start_at, start_local, start_time_zone_id, end_local, end_time_zone_id, leg_kind) VALUES (@legId, @tripId, 'different-owner', 'Mismatched owner stay', TIMESTAMPTZ '2026-01-01 12:00+00', TIMESTAMP '2026-01-01 12:00', 'UTC', TIMESTAMP '2026-01-02 12:00', 'UTC', 'stay')",
            new { legId = mismatchedOwnerLegId, tripId = ownerTrip });
        var mismatched = await repository.GetAuthorizedSourcesAsync(callerId, callerEmail,
            [new TripSearchCandidate(ownerTrip, "leg", mismatchedOwnerLegId, 0)], default);
        Assert.Empty(mismatched);

        await connection.ExecuteAsync("DELETE FROM trip_shares WHERE trip_id = @viewerTrip", new { viewerTrip });
        var afterRevocation = await repository.GetAuthorizedSourcesAsync(callerId, callerEmail, all, default);
        Assert.DoesNotContain(afterRevocation, source => source.TripId == viewerTrip);

        var unindexedTrip = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date) VALUES (@tripId, 'unindexed-owner', 'Needs indexing', DATE '2026-02-01', DATE '2026-02-02')",
            new { tripId = unindexedTrip });
        var batch = await repository.GetIndexBatchAsync(null, "test", 3, 100, default);
        Assert.Contains(batch, source => source.TripId == unindexedTrip && source.SourceKind == "trip");

        var renamedLegId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO trip_legs (trip_leg_id, trip_id, owner_user_id, title, start_at, start_local, start_time_zone_id, end_local, end_time_zone_id, leg_kind) VALUES (@legId, @tripId, 'unindexed-owner', 'Indexed stay', TIMESTAMPTZ '2026-02-01 12:00+00', TIMESTAMP '2026-02-01 12:00', 'UTC', TIMESTAMP '2026-02-02 12:00', 'UTC', 'stay')",
            new { legId = renamedLegId, tripId = unindexedTrip });
        await connection.ExecuteAsync(
            "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) SELECT t.trip_id, t.owner_user_id, 'leg', l.trip_leg_id, 'hash', GREATEST(l.updated_at_utc, t.updated_at_utc), '[0,0,1]'::vector, 'test', 3, now() FROM trip_legs l INNER JOIN trips t ON t.trip_id = l.trip_id WHERE l.trip_leg_id = @legId",
            new { legId = renamedLegId });
        var tripBatch = await repository.GetIndexBatchAsync(unindexedTrip, "test", 3, 100, default);
        Assert.DoesNotContain(tripBatch, source => source.SourceId == renamedLegId);

        await connection.ExecuteAsync("UPDATE trips SET name = 'Renamed trip' WHERE trip_id = @tripId", new { tripId = unindexedTrip });
        tripBatch = await repository.GetIndexBatchAsync(unindexedTrip, "test", 3, 100, default);
        Assert.Contains(tripBatch, source => source.SourceId == renamedLegId && source.TripName == "Renamed trip");

        var orphanSourceId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) VALUES (@tripId, @ownerId, 'leg', @sourceId, 'hash', now(), '[0,1,0]'::vector, 'test', 3, now())",
            new { tripId = ownerTrip, ownerId = callerId, sourceId = orphanSourceId });
        await repository.RemoveOrphanedBatchAsync(10, default);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM trip_search_documents WHERE trip_id = @ownerTrip AND source_id = @sourceId",
            new { ownerTrip, sourceId = orphanSourceId }));
    }
}