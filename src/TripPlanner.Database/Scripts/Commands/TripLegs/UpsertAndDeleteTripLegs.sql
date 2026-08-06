-- US4: owner-scoped trip leg commands. Each statement is delimited by a -- @Name marker
-- so handlers can split and execute the variant they need.

-- @Insert
INSERT INTO trip_legs (trip_leg_id, trip_id, owner_user_id, title, leg_kind, transportation_mode, origin, destination, start_at, end_at, start_local, start_time_zone_id, end_local, end_time_zone_id, travel_cost, confirmation_code, notes, sort_order, created_at_utc, updated_at_utc)
SELECT @TripLegId, @TripId, @OwnerUserId, @Title, @LegKind, @TransportationMode, @Origin, @Destination, @StartAt, @EndAt, @StartLocal, @StartTimeZoneId, @EndLocal, @EndTimeZoneId, @TravelCost, @ConfirmationCode, @Notes, @SortOrder, @NowUtc, @NowUtc
WHERE EXISTS (SELECT 1 FROM trips WHERE trip_id = @TripId AND owner_user_id = @OwnerUserId);

-- @Update
UPDATE trip_legs
SET title = @Title, leg_kind = @LegKind, transportation_mode = @TransportationMode,
    origin = @Origin, destination = @Destination,
    start_at = @StartAt, end_at = @EndAt,
    start_local = @StartLocal, start_time_zone_id = @StartTimeZoneId,
    end_local = @EndLocal, end_time_zone_id = @EndTimeZoneId,
    travel_cost = @TravelCost, confirmation_code = @ConfirmationCode,
    notes = @Notes, sort_order = @SortOrder
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
  destination AS "Destination", start_local AS "StartLocal", start_time_zone_id AS "StartTimeZoneId",
  start_time_zone_id AS "StartTimeZoneLabel", end_local AS "EndLocal", end_time_zone_id AS "EndTimeZoneId",
  end_time_zone_id AS "EndTimeZoneLabel", notes AS "Notes",
       sort_order AS "SortOrder",
       leg_kind AS "LegKind", transportation_mode AS "TransportationMode",
       travel_cost AS "TravelCost", confirmation_code AS "ConfirmationCode"
FROM trip_legs
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY start_local, sort_order;
