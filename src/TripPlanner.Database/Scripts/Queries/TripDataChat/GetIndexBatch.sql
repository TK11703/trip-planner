WITH canonical_sources AS (
    SELECT t.trip_id, t.owner_user_id, 'trip'::text AS source_kind, t.trip_id AS source_id,
        t.name AS trip_name, t.name AS source_label,
        concat_ws(' ', 'Trip:', t.name, 'Destination:', t.destination, 'Description:', t.description,
            'Dates:', t.start_date::text, 'through', t.end_date::text) AS search_text,
        t.updated_at_utc AS source_updated_at_utc
    FROM trips t
    WHERE @TripId IS NULL OR t.trip_id = @TripId

    UNION ALL

    SELECT t.trip_id, t.owner_user_id, 'leg'::text, l.trip_leg_id,
        t.name, l.title,
        concat_ws(' ', 'Trip:', t.name, 'Leg:', l.title, 'Kind:', l.leg_kind,
            'Mode:', l.transportation_mode, 'Origin:', l.origin, 'Destination:', l.destination,
            'Starts:', l.start_local::text, 'Ends:', l.end_local::text, 'Notes:', l.notes),
        -- Leg text embeds the trip name, so trip edits must also refresh it.
        GREATEST(l.updated_at_utc, t.updated_at_utc)
    FROM trip_legs l
    INNER JOIN trips t ON t.trip_id = l.trip_id
    WHERE @TripId IS NULL OR t.trip_id = @TripId

    UNION ALL

    SELECT t.trip_id, t.owner_user_id, 'tracked_item'::text, i.tracked_item_id,
        t.name, i.title,
        concat_ws(' ', 'Trip:', t.name, 'Item type:', i.item_type, 'Item:', i.title,
            'Location:', i.location, 'Starts:', i.start_local::text, 'Ends:', i.end_local::text,
            'Notes:', i.notes),
        GREATEST(i.updated_at_utc, t.updated_at_utc)
    FROM tracked_items i
    INNER JOIN trips t ON t.trip_id = i.trip_id
    WHERE @TripId IS NULL OR t.trip_id = @TripId
)
SELECT
    s.trip_id AS "TripId",
    s.owner_user_id AS "OwnerUserId",
    s.source_kind AS "SourceKind",
    s.source_id AS "SourceId",
    s.trip_name AS "TripName",
    s.source_label AS "SourceLabel",
    s.search_text AS "SearchText",
    s.source_updated_at_utc AS "SourceUpdatedAtUtc"
FROM canonical_sources s
LEFT JOIN trip_search_documents d
    ON d.trip_id = s.trip_id AND d.source_kind = s.source_kind AND d.source_id = s.source_id
WHERE d.search_document_id IS NULL
   OR d.source_updated_at_utc < s.source_updated_at_utc
   OR d.embedding_model <> @EmbeddingModel
   OR d.embedding_dimensions <> @EmbeddingDimensions
ORDER BY s.source_updated_at_utc, s.trip_id, s.source_kind, s.source_id
LIMIT @BatchSize;