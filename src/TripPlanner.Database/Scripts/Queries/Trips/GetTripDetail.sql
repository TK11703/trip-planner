-- Owner-scoped trip detail lookup. The query returns no rows for cross-user IDs so the
-- API can return a generic not-found/denied response without leaking existence.
SELECT
    t.trip_id        AS "TripId",
    t.name           AS "Name",
    t.destination    AS "Destination",
    t.description    AS "Description",
    t.start_date     AS "StartDate",
    t.end_date       AS "EndDate",
    t.created_at_utc AS "CreatedAtUtc",
    t.updated_at_utc AS "UpdatedAtUtc"
FROM trips t
WHERE t.owner_user_id = @OwnerUserId
  AND t.trip_id = @TripId
LIMIT 1;
