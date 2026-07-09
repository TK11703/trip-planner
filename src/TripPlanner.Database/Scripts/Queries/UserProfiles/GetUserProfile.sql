SELECT
    user_id AS UserId,
    first_name AS FirstName,
    last_name AS LastName,
    display_name AS DisplayName,
    email AS Email,
    time_zone_id AS TimeZoneId,
    travel_interests AS TravelInterests,
    home_airport AS HomeAirport,
    preferred_travel_style AS PreferredTravelStyle,
    accessibility_notes AS AccessibilityNotes,
    created_at_utc AS CreatedAtUtc,
    updated_at_utc AS UpdatedAtUtc,
    last_seen_at_utc AS LastSeenAtUtc
FROM users
WHERE user_id = @UserId;
