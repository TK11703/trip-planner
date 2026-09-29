SELECT
    favorite_destination_id AS "FavoriteDestinationId",
    name AS "Name",
    address AS "Address",
    city AS "City",
    country AS "Country",
    latitude AS "Latitude",
    longitude AS "Longitude",
    notes AS "Notes",
    created_at_utc AS "CreatedAtUtc",
    updated_at_utc AS "UpdatedAtUtc"
FROM favorite_destinations
WHERE owner_user_id = @OwnerUserId
  AND favorite_destination_id = @FavoriteDestinationId;