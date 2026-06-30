-- US1: owner-scoped users, trips, audit_events.
CREATE TABLE IF NOT EXISTS users (
    user_id text PRIMARY KEY,
    display_name text NULL,
    email text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    last_seen_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now())
);

CREATE TABLE IF NOT EXISTS trips (
    trip_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_user_id text NOT NULL,
    name text NOT NULL,
    destination text NULL,
    description text NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    updated_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT trips_date_range_chk CHECK (end_date >= start_date)
);

CREATE INDEX IF NOT EXISTS trips_owner_updated_idx ON trips (owner_user_id, updated_at_utc DESC);
CREATE INDEX IF NOT EXISTS trips_owner_trip_idx ON trips (owner_user_id, trip_id);

DROP TRIGGER IF EXISTS trips_set_updated_at ON trips;
CREATE TRIGGER trips_set_updated_at
BEFORE UPDATE ON trips
FOR EACH ROW EXECUTE FUNCTION set_updated_at_utc();

CREATE TABLE IF NOT EXISTS audit_events (
    audit_event_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id text NULL,
    operation text NOT NULL,
    resource_type text NOT NULL,
    resource_id text NULL,
    result text NOT NULL,
    occurred_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now())
);

CREATE INDEX IF NOT EXISTS audit_events_user_time_idx ON audit_events (user_id, occurred_at_utc DESC);
