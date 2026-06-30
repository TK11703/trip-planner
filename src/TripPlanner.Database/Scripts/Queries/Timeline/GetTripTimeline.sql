-- US4: timeline projection. Legs and tracked items are merged into a single ordered
-- list for the trip and only returned for the authenticated owner.
SELECT
    'leg:' || trip_leg_id::text AS "Id",
    'trip-leg'                  AS "SourceType",
    title                       AS "Title",
    start_at                    AS "Start",
    end_at                      AS "End",
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
    false                            AS "AllDay",
    sort_order                       AS "DisplayOrder"
FROM tracked_items
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY "Start", "DisplayOrder";
