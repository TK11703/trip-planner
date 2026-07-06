-- Timezone configuration support for profiles, trip legs, and calendar display.
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS time_zone_id text NULL;

UPDATE users
SET time_zone_id = 'UTC'
WHERE time_zone_id IS NULL OR btrim(time_zone_id) = '';

ALTER TABLE users
    ALTER COLUMN time_zone_id SET DEFAULT 'UTC',
    ALTER COLUMN time_zone_id SET NOT NULL;

ALTER TABLE trip_legs
    ADD COLUMN IF NOT EXISTS start_local timestamp without time zone NULL,
    ADD COLUMN IF NOT EXISTS start_time_zone_id text NULL,
    ADD COLUMN IF NOT EXISTS end_local timestamp without time zone NULL,
    ADD COLUMN IF NOT EXISTS end_time_zone_id text NULL;

UPDATE trip_legs
SET
    start_local = COALESCE(start_local, start_at AT TIME ZONE 'UTC'),
    start_time_zone_id = COALESCE(NULLIF(btrim(start_time_zone_id), ''), 'UTC'),
    end_local = COALESCE(end_local, COALESCE(end_at, start_at) AT TIME ZONE 'UTC'),
    end_time_zone_id = COALESCE(NULLIF(btrim(end_time_zone_id), ''), 'UTC');

ALTER TABLE trip_legs
    ALTER COLUMN start_local SET NOT NULL,
    ALTER COLUMN start_time_zone_id SET NOT NULL,
    ALTER COLUMN end_local SET NOT NULL,
    ALTER COLUMN end_time_zone_id SET NOT NULL;

ALTER TABLE trip_legs
    DROP CONSTRAINT IF EXISTS trip_legs_local_range_chk;

ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_local_range_chk CHECK (end_local >= start_local);

CREATE INDEX IF NOT EXISTS trip_legs_owner_trip_local_start_idx ON trip_legs (owner_user_id, trip_id, start_local, sort_order);