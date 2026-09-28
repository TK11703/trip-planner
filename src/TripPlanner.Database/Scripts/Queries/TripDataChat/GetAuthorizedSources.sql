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
), requested AS (
    SELECT trip_id, source_kind, source_id, ord
    FROM unnest(@TripIds::uuid[], @SourceKinds::text[], @SourceIds::uuid[])
         WITH ORDINALITY AS r(trip_id, source_kind, source_id, ord)
), sources AS (
    SELECT
        r.ord,
        t.trip_id AS "TripId",
        a.owner_user_id AS "OwnerUserId",
        'trip'::text AS "SourceKind",
        t.trip_id AS "SourceId",
        t.name AS "TripName",
        t.name AS "SourceLabel",
        concat_ws(' ', 'Trip:', t.name, 'Destination:', t.destination, 'Description:', t.description,
            'Dates:', t.start_date::text, 'through', t.end_date::text) AS "SearchText",
        t.updated_at_utc AS "SourceUpdatedAtUtc"
    FROM requested r
    INNER JOIN accessible_trips a ON a.trip_id = r.trip_id
    INNER JOIN trips t ON t.trip_id = r.trip_id AND r.source_kind = 'trip' AND r.source_id = t.trip_id

    UNION ALL

    SELECT
        r.ord,
        t.trip_id AS "TripId",
        a.owner_user_id AS "OwnerUserId",
        'leg'::text AS "SourceKind",
        l.trip_leg_id AS "SourceId",
        t.name AS "TripName",
        l.title AS "SourceLabel",
        concat_ws(' ', 'Trip:', t.name, 'Leg:', l.title, 'Kind:', l.leg_kind,
            'Mode:', l.transportation_mode, 'Origin:', l.origin, 'Destination:', l.destination,
            'Starts:', l.start_local::text, 'Ends:', l.end_local::text, 'Notes:', l.notes) AS "SearchText",
        l.updated_at_utc AS "SourceUpdatedAtUtc"
    FROM requested r
    INNER JOIN accessible_trips a ON a.trip_id = r.trip_id
    INNER JOIN trips t ON t.trip_id = r.trip_id
    INNER JOIN trip_legs l ON l.trip_id = t.trip_id AND l.trip_leg_id = r.source_id AND l.owner_user_id = a.owner_user_id
    WHERE r.source_kind = 'leg'

    UNION ALL

    SELECT
        r.ord,
        t.trip_id AS "TripId",
        a.owner_user_id AS "OwnerUserId",
        'tracked_item'::text AS "SourceKind",
        i.tracked_item_id AS "SourceId",
        t.name AS "TripName",
        i.title AS "SourceLabel",
        concat_ws(' ', 'Trip:', t.name, 'Item type:', i.item_type, 'Item:', i.title,
            'Location:', i.location, 'Starts:', i.start_local::text, 'Ends:', i.end_local::text,
            'Notes:', i.notes) AS "SearchText",
        i.updated_at_utc AS "SourceUpdatedAtUtc"
    FROM requested r
    INNER JOIN accessible_trips a ON a.trip_id = r.trip_id
    INNER JOIN trips t ON t.trip_id = r.trip_id
    INNER JOIN tracked_items i ON i.trip_id = t.trip_id AND i.tracked_item_id = r.source_id AND i.owner_user_id = a.owner_user_id
    WHERE r.source_kind = 'tracked_item'
)
SELECT "TripId", "OwnerUserId", "SourceKind", "SourceId", "TripName", "SourceLabel", "SearchText", "SourceUpdatedAtUtc"
FROM sources
ORDER BY ord;