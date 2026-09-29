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
  AND lower(btrim(name)) = lower(btrim(@Name))
  AND lower(btrim(address)) = lower(btrim(@Address))
  AND (@ExcludingFavoriteDestinationId IS NULL
       OR favorite_destination_id <> @ExcludingFavoriteDestinationId)
ORDER BY favorite_destination_id;