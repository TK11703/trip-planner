ALTER TABLE users
    ADD COLUMN IF NOT EXISTS first_name text NULL,
    ADD COLUMN IF NOT EXISTS last_name text NULL,
    ADD COLUMN IF NOT EXISTS updated_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    ADD COLUMN IF NOT EXISTS email_notifications_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS trip_reminder_notifications_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS itinerary_change_notifications_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS notification_updated_at_utc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS travel_interests text NULL,
    ADD COLUMN IF NOT EXISTS home_airport text NULL,
    ADD COLUMN IF NOT EXISTS preferred_travel_style text NULL,
    ADD COLUMN IF NOT EXISTS accessibility_notes text NULL,
    ADD COLUMN IF NOT EXISTS personalization_updated_at_utc timestamptz NULL;

DROP TRIGGER IF EXISTS users_set_updated_at ON users;
CREATE TRIGGER users_set_updated_at
BEFORE UPDATE ON users
FOR EACH ROW EXECUTE FUNCTION set_updated_at_utc();
