-- Feature 010: Trip Sharing and Collaboration.
-- trip_shares grants exactly one access level per (trip, member). Ownership stays on trips.owner_user_id.
CREATE TABLE IF NOT EXISTS trip_shares (
    trip_share_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id uuid NOT NULL REFERENCES trips(trip_id) ON DELETE CASCADE,
    member_user_id text NOT NULL,
    member_display_name text NULL,
    member_email text NULL,
    access_level text NOT NULL,
    created_by_user_id text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    updated_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT trip_shares_access_level_chk CHECK (access_level IN ('viewer','collaborator')),
    CONSTRAINT trip_shares_trip_member_uq UNIQUE (trip_id, member_user_id)
);

-- Look up a member's shares (shared-with-me listing) and a trip's shares quickly.
CREATE INDEX IF NOT EXISTS trip_shares_member_idx ON trip_shares (member_user_id, trip_id);
CREATE INDEX IF NOT EXISTS trip_shares_trip_idx ON trip_shares (trip_id);

DROP TRIGGER IF EXISTS trip_shares_set_updated_at ON trip_shares;
CREATE TRIGGER trip_shares_set_updated_at
BEFORE UPDATE ON trip_shares
FOR EACH ROW EXECUTE FUNCTION set_updated_at_utc();
