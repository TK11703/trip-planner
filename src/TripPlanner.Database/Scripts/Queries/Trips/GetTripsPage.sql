-- Feature 010: paginated trips the caller can access, including trips they own and trips shared
-- with them. Access metadata lets the UI badge each card. Item counts join by trip only because a
-- trip's legs/items all belong to the trip's owner.
WITH accessible AS (
    SELECT
        t.trip_id,
        t.name,
        t.start_date,
        t.end_date,
        t.updated_at_utc,
        'owner'::text AS access_level,
        true          AS is_owner
    FROM trips t
    WHERE t.owner_user_id = @OwnerUserId
    UNION ALL
    SELECT
        t.trip_id,
        t.name,
        t.start_date,
        t.end_date,
        t.updated_at_utc,
        s.access_level,
        false AS is_owner
    FROM trips t
    JOIN trip_shares s ON s.trip_id = t.trip_id
        AND (s.member_user_id = @OwnerUserId
             OR (@CallerEmail IS NOT NULL AND s.member_email IS NOT NULL AND lower(s.member_email) = lower(@CallerEmail)))
),
item_counts AS (
    SELECT trip_id, COUNT(*)::int AS item_count
    FROM trip_legs
    GROUP BY trip_id
    UNION ALL
    SELECT trip_id, COUNT(*)::int AS item_count
    FROM tracked_items
    GROUP BY trip_id
)
SELECT
    a.trip_id        AS "TripId",
    a.name           AS "Name",
    a.start_date     AS "StartDate",
    a.end_date       AS "EndDate",
    a.updated_at_utc AS "UpdatedAtUtc",
    COALESCE(SUM(c.item_count), 0)::int AS "ItemCount",
    a.access_level   AS "AccessLevel",
    a.is_owner       AS "IsOwner",
    COUNT(*) OVER()::int AS "TotalCount"
FROM accessible a
LEFT JOIN item_counts c ON c.trip_id = a.trip_id
GROUP BY a.trip_id, a.name, a.start_date, a.end_date, a.updated_at_utc, a.access_level, a.is_owner
ORDER BY a.updated_at_utc DESC, a.end_date DESC, a.trip_id
LIMIT @Limit OFFSET @Offset;
