UPDATE users
SET
    first_name = @FirstName,
    last_name = @LastName,
    display_name = @DisplayName,
    email = @Email,
    time_zone_id = @TimeZoneId,
    map_provider = @MapProvider,
    notification_updated_at_utc = @NowUtc,
    item_type_colors = COALESCE(CAST(@ItemTypeColors AS jsonb), item_type_colors),
    last_seen_at_utc = @NowUtc
WHERE user_id = @UserId
RETURNING
    user_id AS UserId,
    first_name AS FirstName,
    last_name AS LastName,
    display_name AS DisplayName,
    email AS Email,
    time_zone_id AS TimeZoneId,
    map_provider AS MapProvider,
    item_type_colors::text AS ItemTypeColors,
    created_at_utc AS CreatedAtUtc,
    updated_at_utc AS UpdatedAtUtc,
    last_seen_at_utc AS LastSeenAtUtc;
