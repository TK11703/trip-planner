-- US4: owner-scoped tracked item commands.

-- @Insert
INSERT INTO tracked_items (tracked_item_id, trip_id, trip_leg_id, owner_user_id, item_type, title, location, start_local, start_time_zone_id, starts_at, end_local, end_time_zone_id, ends_at, display_color, confirmation_code, notes, sort_order, created_at_utc, updated_at_utc)
SELECT @TrackedItemId, @TripId, @TripLegId, @OwnerUserId, @ItemType, @Title, @Location, @StartLocal, @StartTimeZoneId, @StartsAt, @EndLocal, @EndTimeZoneId, @EndsAt, @DisplayColor, @ConfirmationCode, @Notes, @SortOrder, @NowUtc, @NowUtc
WHERE EXISTS (SELECT 1 FROM trips WHERE trip_id = @TripId AND owner_user_id = @OwnerUserId)
  AND (@TripLegId IS NULL OR EXISTS (SELECT 1 FROM trip_legs WHERE trip_leg_id = @TripLegId AND trip_id = @TripId AND owner_user_id = @OwnerUserId));

-- @Update
UPDATE tracked_items
SET trip_leg_id = @TripLegId, item_type = @ItemType, title = @Title, location = @Location,
    start_local = @StartLocal, start_time_zone_id = @StartTimeZoneId, starts_at = @StartsAt,
    end_local = @EndLocal, end_time_zone_id = @EndTimeZoneId, ends_at = @EndsAt, display_color = @DisplayColor,
    confirmation_code = @ConfirmationCode, notes = @Notes, sort_order = @SortOrder
WHERE tracked_item_id = @TrackedItemId
  AND trip_id = @TripId
  AND owner_user_id = @OwnerUserId
  AND (@TripLegId IS NULL OR EXISTS (SELECT 1 FROM trip_legs WHERE trip_leg_id = @TripLegId AND trip_id = @TripId AND owner_user_id = @OwnerUserId));

-- @Delete
DELETE FROM tracked_items
WHERE tracked_item_id = @TrackedItemId
  AND trip_id = @TripId
  AND owner_user_id = @OwnerUserId;

-- @SelectByTrip
SELECT tracked_item_id AS "TrackedItemId", trip_id AS "TripId", trip_leg_id AS "TripLegId", item_type AS "ItemType",
       title AS "Title", location AS "Location",
       start_local AS "StartLocal", start_time_zone_id AS "StartTimeZoneId", starts_at AS "StartsAt",
       end_local AS "EndLocal", end_time_zone_id AS "EndTimeZoneId", ends_at AS "EndsAt",
       display_color AS "DisplayColor", confirmation_code AS "ConfirmationCode", notes AS "Notes", sort_order AS "SortOrder"
FROM tracked_items
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY starts_at, sort_order;

-- @CountByLeg
SELECT COUNT(*)
FROM tracked_items
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId AND trip_leg_id = @TripLegId;
