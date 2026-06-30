-- US3: Insert a trip owned by the authenticated user. owner_user_id is sourced
-- exclusively from validated authentication context, never from request payloads.
INSERT INTO trips (trip_id, owner_user_id, name, destination, description, start_date, end_date, created_at_utc, updated_at_utc)
VALUES (@TripId, @OwnerUserId, @Name, @Destination, @Description, @StartDate, @EndDate, @CreatedAtUtc, @CreatedAtUtc)
RETURNING trip_id;
