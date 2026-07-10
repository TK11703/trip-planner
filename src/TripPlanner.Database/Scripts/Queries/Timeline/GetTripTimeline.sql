-- 007: resource timeline projection. Returns two result sets scoped to the owner:
--   (1) trip legs ordered chronologically, acting as timeline resource rows.
--   (2) tracked items with their leg assignment and display color.
-- The repository groups items under their leg, flags out-of-leg-range items, and
-- collects unassigned (legacy) items.

-- Legs
SELECT
    trip_leg_id                                     AS "TripLegId",
    title                                           AS "Title",
    origin                                          AS "Origin",
    destination                                     AS "Destination",
    start_local                                     AS "StartLocal",
    start_time_zone_id                              AS "StartTimeZoneId",
    end_local                                       AS "EndLocal",
    end_time_zone_id                                AS "EndTimeZoneId",
    start_at                                        AS "StartAt",
    COALESCE(end_at, start_at)                      AS "EndAt",
    sort_order                                      AS "SortOrder"
FROM trip_legs
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY start_at, sort_order, title, trip_leg_id;

-- Items
SELECT
    tracked_item_id                                 AS "TrackedItemId",
    trip_leg_id                                     AS "TripLegId",
    item_type                                       AS "ItemType",
    title                                           AS "Title",
    location                                        AS "Location",
    start_local                                     AS "StartLocal",
    start_time_zone_id                              AS "StartTimeZoneId",
    starts_at                                       AS "StartsAt",
    end_local                                       AS "EndLocal",
    end_time_zone_id                                AS "EndTimeZoneId",
    ends_at                                         AS "EndsAt",
    display_color                                   AS "DisplayColor",
    sort_order                                      AS "SortOrder",
    estimated_cost                                  AS "EstimatedCost"
FROM tracked_items
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY starts_at, sort_order, title, tracked_item_id;
