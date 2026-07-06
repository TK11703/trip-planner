WITH scoped_trip AS (
    SELECT trip_id
    FROM trips
    WHERE trip_id = @TripId AND owner_user_id = @OwnerUserId
),
last_leg AS (
    SELECT end_time_zone_id
    FROM trip_legs
    WHERE trip_id = @TripId AND owner_user_id = @OwnerUserId
    ORDER BY start_local DESC, sort_order DESC, created_at_utc DESC
    LIMIT 1
)
SELECT
    COALESCE(last_leg.end_time_zone_id, users.time_zone_id) AS "StartTimeZoneId",
    COALESCE(last_leg.end_time_zone_id, users.time_zone_id) AS "EndTimeZoneId",
    CASE WHEN last_leg.end_time_zone_id IS NULL THEN 'profile' ELSE 'last-trip-leg' END AS "Source"
FROM scoped_trip
JOIN users ON users.user_id = @OwnerUserId
LEFT JOIN last_leg ON true;