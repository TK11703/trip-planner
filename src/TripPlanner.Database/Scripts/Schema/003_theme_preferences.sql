CREATE TABLE IF NOT EXISTS theme_preferences (
    traveler_id text PRIMARY KEY,
    theme_mode text NOT NULL CHECK (theme_mode IN ('light', 'dark')),
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now()
);
