INSERT INTO users (user_id, first_name, last_name, display_name, email, created_at_utc, updated_at_utc, last_seen_at_utc)
VALUES (@UserId, @FirstName, @LastName, @DisplayName, @Email, @NowUtc, @NowUtc, @NowUtc)
ON CONFLICT (user_id) DO UPDATE
SET last_seen_at_utc = EXCLUDED.last_seen_at_utc
RETURNING
    user_id AS UserId,
    first_name AS FirstName,
    last_name AS LastName,
    display_name AS DisplayName,
    email AS Email,
    time_zone_id AS TimeZoneId,
    map_provider AS MapProvider,
    travel_interests AS TravelInterests,
    home_airport AS HomeAirport,
    preferred_travel_style AS PreferredTravelStyle,
    accessibility_notes AS AccessibilityNotes,
    created_at_utc AS CreatedAtUtc,
    updated_at_utc AS UpdatedAtUtc,
    last_seen_at_utc AS LastSeenAtUtc;
