SELECT traveler_id AS TravelerId,
       theme_mode AS ThemeMode,
       created_at_utc AS CreatedAtUtc,
       updated_at_utc AS UpdatedAtUtc
FROM theme_preferences
WHERE traveler_id = @TravelerId;
