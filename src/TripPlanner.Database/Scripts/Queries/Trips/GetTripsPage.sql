-- Owner-scoped paginated trip listing for the current user. The count is scoped
-- to the same owner filter so metadata never includes another user's trips.
WITH owner_trips AS (
    SELECT
        t.trip_id,
        t.name,
        t.start_date,
        t.end_date,
        t.updated_at_utc
    FROM trips t
    WHERE t.owner_user_id = @OwnerUserId
),
item_counts AS (
    SELECT trip_id, COUNT(*)::int AS item_count
    FROM trip_legs
    WHERE owner_user_id = @OwnerUserId
    GROUP BY trip_id
    UNION ALL
    SELECT trip_id, COUNT(*)::int AS item_count
    FROM tracked_items
    WHERE owner_user_id = @OwnerUserId
    GROUP BY trip_id
)
SELECT
    t.trip_id        AS "TripId",
    t.name           AS "Name",
    t.start_date     AS "StartDate",
    t.end_date       AS "EndDate",
    t.updated_at_utc AS "UpdatedAtUtc",
    COALESCE(SUM(c.item_count), 0)::int AS "ItemCount",
    COUNT(*) OVER()::int AS "TotalCount"
FROM owner_trips t
LEFT JOIN item_counts c ON c.trip_id = t.trip_id
GROUP BY t.trip_id, t.name, t.start_date, t.end_date, t.updated_at_utc
ORDER BY t.updated_at_utc DESC, t.end_date DESC, t.trip_id
LIMIT @Limit OFFSET @Offset;
