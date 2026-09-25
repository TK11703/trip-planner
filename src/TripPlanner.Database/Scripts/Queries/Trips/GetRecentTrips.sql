-- Owner-scoped recent trips for the current user. Item count is a left join aggregate
-- across legs and tracked items so the schema can change additively. Description is capped
-- because cards only show a short, clamped preview.
SELECT
    t.trip_id        AS "TripId",
    t.name           AS "Name",
    left(t.description, 500) AS "Description",
    t.start_date     AS "StartDate",
    t.end_date       AS "EndDate",
    t.updated_at_utc AS "UpdatedAtUtc",
    COALESCE(legs.item_count, 0) + COALESCE(items.item_count, 0) AS "ItemCount"
FROM trips t
LEFT JOIN (
    SELECT trip_id, COUNT(*)::int AS item_count
    FROM trip_legs
    WHERE owner_user_id = @OwnerUserId
    GROUP BY trip_id
) legs ON legs.trip_id = t.trip_id
LEFT JOIN (
    SELECT trip_id, COUNT(*)::int AS item_count
    FROM tracked_items
    WHERE owner_user_id = @OwnerUserId
    GROUP BY trip_id
) items ON items.trip_id = t.trip_id
WHERE t.owner_user_id = @OwnerUserId
ORDER BY t.updated_at_utc DESC, t.end_date DESC
LIMIT @Limit;
