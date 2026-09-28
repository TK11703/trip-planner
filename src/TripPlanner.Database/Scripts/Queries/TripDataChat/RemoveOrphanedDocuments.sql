WITH orphans AS (
    SELECT d.search_document_id
    FROM trip_search_documents d
    WHERE NOT EXISTS (
        SELECT 1 FROM trips t
        WHERE t.trip_id = d.trip_id
          AND (
                (d.source_kind = 'trip' AND d.source_id = t.trip_id)
                OR (d.source_kind = 'leg' AND EXISTS (
                    SELECT 1 FROM trip_legs l WHERE l.trip_id = t.trip_id AND l.trip_leg_id = d.source_id
                ))
                OR (d.source_kind = 'tracked_item' AND EXISTS (
                    SELECT 1 FROM tracked_items i WHERE i.trip_id = t.trip_id AND i.tracked_item_id = d.source_id
                ))
          )
    )
    ORDER BY d.trip_id, d.source_kind, d.source_id
    LIMIT @BatchSize
    FOR UPDATE SKIP LOCKED
)
DELETE FROM trip_search_documents d
USING orphans o
WHERE d.search_document_id = o.search_document_id;