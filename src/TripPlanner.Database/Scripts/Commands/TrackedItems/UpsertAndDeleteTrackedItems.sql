-- US4: owner-scoped tracked item commands.

-- @Insert
INSERT INTO tracked_items (tracked_item_id, trip_id, owner_user_id, item_type, title, location, starts_at, ends_at, confirmation_code, notes, sort_order, created_at_utc, updated_at_utc)
SELECT @TrackedItemId, @TripId, @OwnerUserId, @ItemType, @Title, @Location, @StartsAt, @EndsAt, @ConfirmationCode, @Notes, @SortOrder, @NowUtc, @NowUtc
WHERE EXISTS (SELECT 1 FROM trips WHERE trip_id = @TripId AND owner_user_id = @OwnerUserId);

-- @Update
UPDATE tracked_items
SET item_type = @ItemType, title = @Title, location = @Location,
    starts_at = @StartsAt, ends_at = @EndsAt,
    confirmation_code = @ConfirmationCode, notes = @Notes, sort_order = @SortOrder
WHERE tracked_item_id = @TrackedItemId
  AND trip_id = @TripId
  AND owner_user_id = @OwnerUserId;

-- @Delete
DELETE FROM tracked_items
WHERE tracked_item_id = @TrackedItemId
  AND trip_id = @TripId
  AND owner_user_id = @OwnerUserId;

-- @SelectByTrip
SELECT tracked_item_id AS "TrackedItemId", trip_id AS "TripId", item_type AS "ItemType",
       title AS "Title", location AS "Location", starts_at AS "StartsAt", ends_at AS "EndsAt",
       confirmation_code AS "ConfirmationCode", notes AS "Notes", sort_order AS "SortOrder"
FROM tracked_items
WHERE owner_user_id = @OwnerUserId AND trip_id = @TripId
ORDER BY starts_at, sort_order;
