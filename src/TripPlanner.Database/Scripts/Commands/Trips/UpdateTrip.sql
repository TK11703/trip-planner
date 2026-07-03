-- US3: Owner-scoped trip update. Returns affected row count so handlers can
-- distinguish "no such trip / not owned" (0) from a successful update (1).
UPDATE trips
SET name = @Name,
    destination = NULL,
    description = @Description,
    start_date = @StartDate,
    end_date = @EndDate
WHERE trip_id = @TripId
  AND owner_user_id = @OwnerUserId;
