-- 007: Relate tracked items to trip legs and add a selectable display color.
-- trip_leg_id uses ON DELETE SET NULL so that deleting an entire trip can still
-- cascade (trips -> trip_legs and trips -> tracked_items) without a restrict
-- conflict. Direct leg deletion while items still reference the leg is blocked in
-- the API layer, which returns a friendly validation error instead.
ALTER TABLE tracked_items
    ADD COLUMN IF NOT EXISTS trip_leg_id uuid NULL REFERENCES trip_legs(trip_leg_id) ON DELETE SET NULL,
    ADD COLUMN IF NOT EXISTS display_color text NULL;

UPDATE tracked_items
SET display_color = 'slate'
WHERE display_color IS NULL OR btrim(display_color) = '';

ALTER TABLE tracked_items
    ALTER COLUMN display_color SET DEFAULT 'slate',
    ALTER COLUMN display_color SET NOT NULL;

CREATE INDEX IF NOT EXISTS tracked_items_owner_trip_leg_idx
    ON tracked_items (owner_user_id, trip_id, trip_leg_id, starts_at, sort_order);
