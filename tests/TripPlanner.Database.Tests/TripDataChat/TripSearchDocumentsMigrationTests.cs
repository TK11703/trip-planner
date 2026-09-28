using Dapper;
using Npgsql;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;

namespace TripPlanner.Database.Tests.TripDataChat;

[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public sealed class TripSearchDocumentsMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task MigrationCreatesConstrainedVectorSidecarAndIsRepeatable()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        Assert.Equal("vector", await connection.ExecuteScalarAsync<string?>(
            "SELECT udt_name FROM information_schema.columns WHERE table_name = 'trip_search_documents' AND column_name = 'embedding'"));

        var migration = new SqlFileProvider().Get("Schema/017_trip_search_documents.sql");
        await connection.ExecuteAsync(migration);

        var tripId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date) VALUES (@tripId, 'chat-migration-owner', 'Canonical trip', DATE '2026-01-01', DATE '2026-01-02')",
            new { tripId });
        await connection.ExecuteAsync(
            "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) VALUES (@tripId, 'chat-migration-owner', 'trip', @sourceId, 'hash', now(), '[0.1,0.2,0.3]'::vector, 'embedding-deployment', 3, now())",
            new { tripId, sourceId });

        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM trips WHERE trip_id = @tripId", new { tripId }));
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM trip_search_documents WHERE trip_id = @tripId", new { tripId }));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) VALUES (@tripId, 'chat-migration-owner', 'trip', @sourceId, 'hash', now(), '[0.1,0.2,0.3]'::vector, 'embedding-deployment', 3, now())",
            new { tripId, sourceId }));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) VALUES (@tripId, 'chat-migration-owner', 'trip', @sourceId, 'hash', now(), '[0.1,0.2,0.3]'::vector, 'embedding-deployment', 2, now())",
            new { tripId, sourceId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) VALUES (@tripId, 'chat-migration-owner', 'trip', @sourceId, 'hash', now(), '[0.1,0.2,0.3]'::vector, 'embedding-deployment', 3, now())",
            new { tripId = Guid.NewGuid(), sourceId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<PostgresException>(() => connection.ExecuteAsync(
            "INSERT INTO trip_search_documents (trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc, embedding, embedding_model, embedding_dimensions, embedded_at_utc) VALUES (@tripId, 'chat-migration-owner', 'unknown', @sourceId, 'hash', now(), '[0.1,0.2,0.3]'::vector, 'embedding-deployment', 3, now())",
            new { tripId, sourceId = Guid.NewGuid() }));
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'trip_search_documents' AND column_name IN ('source_text', 'confirmation_code'))"));

        await connection.ExecuteAsync("DELETE FROM trips WHERE trip_id = @tripId", new { tripId });
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM trip_search_documents WHERE trip_id = @tripId", new { tripId }));
    }
}