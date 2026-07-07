-- Owner-scoped trip delete. Cascades remove trip legs, tracked items, and shares;
-- notifications referencing the trip have their related trip cleared (ON DELETE SET NULL).
-- Returns affected row count so handlers can distinguish "no such trip / not owned" (0) from success (1).
DELETE FROM trips
WHERE trip_id = @TripId
  AND owner_user_id = @OwnerUserId;
