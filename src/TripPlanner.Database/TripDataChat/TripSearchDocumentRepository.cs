using Dapper;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.TripDataChat;

public sealed class TripSearchDocumentRepository(
    IPostgresConnectionFactory connections,
    ISqlFileProvider sql) : ITripSearchDocumentRepository
{
    public async Task<bool> HasAccessibleTripsAsync(string callerUserId, string? callerEmail, CancellationToken cancellationToken)
    {
        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql.Get("Queries/TripDataChat/GetAccessibleTripCount.sql"),
            new { CallerUserId = callerUserId, CallerEmail = callerEmail },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TripSearchCandidate>> FindAuthorizedCandidatesAsync(
        string callerUserId, string? callerEmail, string embedding, int embeddingDimensions, int topK, CancellationToken cancellationToken)
    {
        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        var candidates = await connection.QueryAsync<TripSearchCandidate>(new CommandDefinition(
            sql.Get("Queries/TripDataChat/FindAuthorizedCandidates.sql"),
            new
            {
                CallerUserId = callerUserId,
                CallerEmail = callerEmail,
                Embedding = embedding,
                EmbeddingDimensions = embeddingDimensions,
                TopK = topK
            },
            cancellationToken: cancellationToken));
        return candidates.AsList();
    }

    public async Task<IReadOnlyList<TripSearchSource>> GetAuthorizedSourcesAsync(
        string callerUserId, string? callerEmail, IReadOnlyList<TripSearchCandidate> candidates, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0) return [];

        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        var sources = await connection.QueryAsync<TripSearchSource>(new CommandDefinition(
            sql.Get("Queries/TripDataChat/GetAuthorizedSources.sql"),
            new
            {
                CallerUserId = callerUserId,
                CallerEmail = callerEmail,
                TripIds = candidates.Select(c => c.TripId).ToArray(),
                SourceKinds = candidates.Select(c => c.SourceKind).ToArray(),
                SourceIds = candidates.Select(c => c.SourceId).ToArray()
            },
            cancellationToken: cancellationToken));
        return sources.AsList();
    }

    public async Task<IReadOnlyList<TripSearchSource>> GetIndexBatchAsync(
        Guid? tripId, string embeddingModel, int embeddingDimensions, int batchSize, CancellationToken cancellationToken)
    {
        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        var sources = await connection.QueryAsync<TripSearchSource>(new CommandDefinition(
            sql.Get("Queries/TripDataChat/GetIndexBatch.sql"),
            new { TripId = tripId, EmbeddingModel = embeddingModel, EmbeddingDimensions = embeddingDimensions, BatchSize = batchSize },
            cancellationToken: cancellationToken));
        return sources.AsList();
    }

    public async Task UpsertAsync(TripSearchDocumentWrite document, CancellationToken cancellationToken)
    {
        const string query = """
            INSERT INTO trip_search_documents (
                trip_id, owner_user_id, source_kind, source_id, content_hash, source_updated_at_utc,
                embedding, embedding_model, embedding_dimensions, embedded_at_utc)
            VALUES (
                @TripId, @OwnerUserId, @SourceKind, @SourceId, @ContentHash, @SourceUpdatedAtUtc,
                CAST(@Embedding AS vector), @EmbeddingModel, @EmbeddingDimensions, @EmbeddedAtUtc)
            ON CONFLICT (trip_id, source_kind, source_id) DO UPDATE SET
                owner_user_id = EXCLUDED.owner_user_id,
                content_hash = EXCLUDED.content_hash,
                source_updated_at_utc = EXCLUDED.source_updated_at_utc,
                embedding = EXCLUDED.embedding,
                embedding_model = EXCLUDED.embedding_model,
                embedding_dimensions = EXCLUDED.embedding_dimensions,
                embedded_at_utc = EXCLUDED.embedded_at_utc;
            """;
        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(query, new
        {
            document.Source.TripId,
            document.Source.OwnerUserId,
            document.Source.SourceKind,
            document.Source.SourceId,
            document.ContentHash,
            document.Source.SourceUpdatedAtUtc,
            document.Embedding,
            document.EmbeddingModel,
            document.EmbeddingDimensions,
            document.EmbeddedAtUtc
        }, cancellationToken: cancellationToken));
    }

    public async Task DeleteSourceAsync(Guid tripId, string sourceKind, Guid sourceId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM trip_search_documents WHERE trip_id = @TripId AND source_kind = @SourceKind AND source_id = @SourceId",
            new { TripId = tripId, SourceKind = sourceKind, SourceId = sourceId },
            cancellationToken: cancellationToken));
    }

    public async Task DeleteTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM trip_search_documents WHERE trip_id = @TripId",
            new { TripId = tripId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> RemoveOrphanedBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var connection = await connections.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            sql.Get("Queries/TripDataChat/RemoveOrphanedDocuments.sql"),
            new { BatchSize = batchSize },
            cancellationToken: cancellationToken));
    }
}