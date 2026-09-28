WITH accessible_trips AS MATERIALIZED (
    SELECT t.trip_id, t.owner_user_id
    FROM trips t
    LEFT JOIN trip_shares s ON s.trip_id = t.trip_id
        AND (
            s.member_user_id = @CallerUserId
            OR (
                @CallerEmail IS NOT NULL
                AND s.member_email IS NOT NULL
                AND lower(s.member_email) = lower(@CallerEmail)
            )
        )
    WHERE t.owner_user_id = @CallerUserId
       OR s.member_user_id = @CallerUserId
       OR (
            @CallerEmail IS NOT NULL
            AND s.member_email IS NOT NULL
            AND lower(s.member_email) = lower(@CallerEmail)
       )
), eligible_documents AS MATERIALIZED (
    SELECT
        d.trip_id,
        d.source_kind,
        d.source_id,
        d.embedding <=> CAST(@Embedding AS vector) AS distance
    FROM trip_search_documents d
    INNER JOIN accessible_trips a
        ON a.trip_id = d.trip_id
       AND a.owner_user_id = d.owner_user_id
    WHERE d.embedding_dimensions = @EmbeddingDimensions
      AND vector_dims(d.embedding) = @EmbeddingDimensions
)
SELECT
    trip_id AS "TripId",
    source_kind AS "SourceKind",
    source_id AS "SourceId",
    distance AS "Distance"
FROM eligible_documents
ORDER BY distance, trip_id, source_kind, source_id
LIMIT @TopK;