-- 008: event-level start/end local times and timezone selections, plus length
-- limits for confirmation/reservation code and notes. Existing rows are
-- backfilled from their stored instants so start_local/start_time_zone_id can
-- become NOT NULL while end_local/end_time_zone_id stay optional.
ALTER TABLE tracked_items
    ADD COLUMN IF NOT EXISTS start_local timestamp without time zone NULL,
    ADD COLUMN IF NOT EXISTS start_time_zone_id text NULL,
    ADD COLUMN IF NOT EXISTS end_local timestamp without time zone NULL,
    ADD COLUMN IF NOT EXISTS end_time_zone_id text NULL;

UPDATE tracked_items
SET
    start_local = COALESCE(start_local, starts_at AT TIME ZONE 'UTC'),
    start_time_zone_id = COALESCE(NULLIF(btrim(start_time_zone_id), ''), 'UTC'),
    end_local = COALESCE(end_local, CASE WHEN ends_at IS NULL THEN NULL ELSE ends_at AT TIME ZONE 'UTC' END),
    end_time_zone_id = CASE
        WHEN ends_at IS NULL THEN end_time_zone_id
        ELSE COALESCE(NULLIF(btrim(end_time_zone_id), ''), 'UTC')
    END;

ALTER TABLE tracked_items
    ALTER COLUMN start_local SET NOT NULL,
    ALTER COLUMN start_time_zone_id SET NOT NULL,
    ALTER COLUMN start_time_zone_id SET DEFAULT 'UTC';

ALTER TABLE tracked_items
    DROP CONSTRAINT IF EXISTS tracked_items_confirmation_len_chk,
    DROP CONSTRAINT IF EXISTS tracked_items_notes_len_chk;

ALTER TABLE tracked_items
    ADD CONSTRAINT tracked_items_confirmation_len_chk CHECK (confirmation_code IS NULL OR char_length(confirmation_code) <= 255),
    ADD CONSTRAINT tracked_items_notes_len_chk CHECK (notes IS NULL OR char_length(notes) <= 2000);

CREATE INDEX IF NOT EXISTS tracked_items_owner_trip_local_start_idx
    ON tracked_items (owner_user_id, trip_id, start_local, sort_order);
