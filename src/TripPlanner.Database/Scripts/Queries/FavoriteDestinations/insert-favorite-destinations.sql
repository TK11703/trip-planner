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
SELECT
    row.favorite_destination_id,
    @OwnerUserId,
    row.name,
    row.address,
    row.city,
    row.country,
    row.latitude,
    row.longitude,
    row.notes,
    @NowUtc,
    @NowUtc
FROM unnest(
    @FavoriteDestinationIds::uuid[],
    @Names::text[],
    @Addresses::text[],
    @Cities::text[],
    @Countries::text[],
    @Latitudes::double precision[],
    @Longitudes::double precision[],
    @NotesValues::text[]
) AS row(favorite_destination_id, name, address, city, country, latitude, longitude, notes)
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