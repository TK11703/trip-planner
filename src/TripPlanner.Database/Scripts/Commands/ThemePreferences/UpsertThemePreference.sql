INSERT INTO theme_preferences (traveler_id, theme_mode, created_at_utc, updated_at_utc)
VALUES (@TravelerId, @ThemeMode, @NowUtc, @NowUtc)
ON CONFLICT (traveler_id)
DO UPDATE SET theme_mode = EXCLUDED.theme_mode,
              updated_at_utc = EXCLUDED.updated_at_utc
RETURNING traveler_id AS TravelerId,
          theme_mode AS ThemeMode,
          created_at_utc AS CreatedAtUtc,
          updated_at_utc AS UpdatedAtUtc;
