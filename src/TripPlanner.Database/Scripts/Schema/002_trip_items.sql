-- US4: trip_legs and tracked_items, owner-scoped with date indexes.
CREATE TABLE IF NOT EXISTS trip_legs (
    trip_leg_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id uuid NOT NULL REFERENCES trips(trip_id) ON DELETE CASCADE,
    owner_user_id text NOT NULL,
    title text NOT NULL,
    origin text NULL,
    destination text NULL,
    start_at timestamptz NOT NULL,
    end_at timestamptz NULL,
    notes text NULL,
    sort_order integer NOT NULL DEFAULT 0,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    updated_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT trip_legs_range_chk CHECK (end_at IS NULL OR end_at >= start_at)
);

CREATE INDEX IF NOT EXISTS trip_legs_owner_trip_start_idx ON trip_legs (owner_user_id, trip_id, start_at, sort_order);

DROP TRIGGER IF EXISTS trip_legs_set_updated_at ON trip_legs;
CREATE TRIGGER trip_legs_set_updated_at BEFORE UPDATE ON trip_legs FOR EACH ROW EXECUTE FUNCTION set_updated_at_utc();

CREATE TABLE IF NOT EXISTS tracked_items (
    tracked_item_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id uuid NOT NULL REFERENCES trips(trip_id) ON DELETE CASCADE,
    owner_user_id text NOT NULL,
    item_type text NOT NULL,
    title text NOT NULL,
    location text NULL,
    starts_at timestamptz NOT NULL,
    ends_at timestamptz NULL,
    confirmation_code text NULL,
    notes text NULL,
    sort_order integer NOT NULL DEFAULT 0,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    updated_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT tracked_items_range_chk CHECK (ends_at IS NULL OR ends_at >= starts_at),
    CONSTRAINT tracked_items_type_chk CHECK (item_type IN ('event','reservation','activity','reminder'))
);

CREATE INDEX IF NOT EXISTS tracked_items_owner_trip_start_idx ON tracked_items (owner_user_id, trip_id, starts_at, sort_order);

DROP TRIGGER IF EXISTS tracked_items_set_updated_at ON tracked_items;
CREATE TRIGGER tracked_items_set_updated_at BEFORE UPDATE ON tracked_items FOR EACH ROW EXECUTE FUNCTION set_updated_at_utc();
