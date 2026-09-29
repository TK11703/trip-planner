UPDATE favorite_destinations
SET
    name = @Name,
    address = @Address,
    city = @City,
    country = @Country,
    latitude = @Latitude,
    longitude = @Longitude,
    notes = @Notes,
    updated_at_utc = @NowUtc
WHERE owner_user_id = @OwnerUserId
  AND favorite_destination_id = @FavoriteDestinationId
RETURNING
    favorite_destination_id AS "FavoriteDestinationId",
    name AS "Name",
    address AS "Address",
    city AS "City",
    country AS "Country",
    latitude AS "Latitude",
    longitude AS "Longitude",
    notes AS "Notes",
    created_at_utc AS "CreatedAtUtc",
    updated_at_utc AS "UpdatedAtUtc";