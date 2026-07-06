-- US4: timeline projection. Legs and tracked items are merged into a single ordered
-- list for the trip and only returned for the authenticated owner.
SELECT
    'leg:' || trip_leg_id::text AS "Id",
    'trip-leg'                  AS "SourceType",
    title                       AS "Title",
    start_at                    AS "Start",
    end_at                      AS "End",
    to_char(start_local, 'YYYY-MM-DD"T"HH24:MI:SS') AS "CalendarStart",
    to_char(end_local, 'YYYY-MM-DD"T"HH24:MI:SS')   AS "CalendarEnd",
    start_time_zone_id          AS "StartTimeZoneId",
    start_time_zone_id          AS "StartTimeZoneLabel",
    end_time_zone_id            AS "EndTimeZoneId",
    end_time_zone_id            AS "EndTimeZoneLabel",
    false                       AS "AllDay",
    sort_order                  AS "DisplayOrder"
FROM trip_legs
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
UNION ALL
SELECT
    'item:' || tracked_item_id::text AS "Id",
    'tracked-item'                   AS "SourceType",
    title                            AS "Title",
    starts_at                        AS "Start",
    ends_at                          AS "End",
    to_char(starts_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS') AS "CalendarStart",
    CASE
        WHEN ends_at IS NULL THEN NULL
        ELSE to_char(ends_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS')
    END                              AS "CalendarEnd",
    NULL::text                       AS "StartTimeZoneId",
    NULL::text                       AS "StartTimeZoneLabel",
    NULL::text                       AS "EndTimeZoneId",
    NULL::text                       AS "EndTimeZoneLabel",
    false                            AS "AllDay",
    sort_order                       AS "DisplayOrder"
FROM tracked_items
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY "Start", "DisplayOrder";
