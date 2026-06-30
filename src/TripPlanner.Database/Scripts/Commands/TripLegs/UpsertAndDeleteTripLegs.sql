-- US4: owner-scoped trip leg commands. Each statement is delimited by a -- @Name marker
-- so handlers can split and execute the variant they need.

-- @Insert
INSERT INTO trip_legs (trip_leg_id, trip_id, owner_user_id, title, origin, destination, start_at, end_at, notes, sort_order, created_at_utc, updated_at_utc)
SELECT @TripLegId, @TripId, @OwnerUserId, @Title, @Origin, @Destination, @StartAt, @EndAt, @Notes, @SortOrder, @NowUtc, @NowUtc
WHERE EXISTS (SELECT 1 FROM trips WHERE trip_id = @TripId AND owner_user_id = @OwnerUserId);

-- @Update
UPDATE trip_legs
SET title = @Title, origin = @Origin, destination = @Destination,
    start_at = @StartAt, end_at = @EndAt, notes = @Notes, sort_order = @SortOrder
WHERE trip_leg_id = @TripLegId
  AND trip_id = @TripId
  AND owner_user_id = @OwnerUserId;

-- @Delete
DELETE FROM trip_legs
WHERE trip_leg_id = @TripLegId
  AND trip_id = @TripId
  AND owner_user_id = @OwnerUserId;

-- @SelectByTrip
SELECT trip_leg_id AS "TripLegId", trip_id AS "TripId", title AS "Title", origin AS "Origin",
       destination AS "Destination", start_at AS "StartAt", end_at AS "EndAt", notes AS "Notes",
       sort_order AS "SortOrder"
FROM trip_legs
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY start_at, sort_order;
