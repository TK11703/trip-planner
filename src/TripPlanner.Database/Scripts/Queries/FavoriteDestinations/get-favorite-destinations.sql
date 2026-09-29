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
    AND (
            @SearchPattern IS NULL
            OR concat_ws(E'\n', name, address, city, country, notes) ILIKE @SearchPattern ESCAPE E'\\'
    )
ORDER BY
    CASE WHEN NULLIF(btrim(country), '') IS NULL THEN 1 ELSE 0 END,
    lower(btrim(country)),
    CASE WHEN NULLIF(btrim(city), '') IS NULL THEN 1 ELSE 0 END,
    lower(btrim(city)),
    lower(btrim(name)),
    favorite_destination_id;