INSERT INTO favorite_destinations (
    favorite_destination_id,
    owner_user_id,
    name,
    address,
    city,
    country,
    latitude,
    longitude,
    notes,
    created_at_utc,
    updated_at_utc
)
VALUES (
    @FavoriteDestinationId,
    @OwnerUserId,
    @Name,
    @Address,
    @City,
    @Country,
    @Latitude,
    @Longitude,
    @Notes,
    @NowUtc,
    @NowUtc
)
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