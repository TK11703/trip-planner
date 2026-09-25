-- 016: Let a recognized booking propose a trip leg instead of a tracked item.
--
-- Feature 028. Recognition previously sorted every booking into one bucket that could only ever
-- become a tracked item, while feature 025 made flight, train, bus, and boat legs reject items
-- outright. A forwarded flight therefore became a reservation that could not be attached to the
-- leg it described. These columns carry the proposed outcome and the route details a travel leg
-- needs, so confirmation can create a leg.
--
-- Written defensively with ADD COLUMN IF NOT EXISTS / DROP CONSTRAINT IF EXISTS, matching the
-- house style of 014. Since feature 026, DatabaseInitializer applies each script exactly once
-- and records it in the schema_migrations ledger with a checksum, so this script is not replayed
-- and the guards are belt-and-braces rather than load-bearing.
--
-- That ledger also means this file must be correct before it is first applied anywhere: editing
-- an already-applied migration raises MigrationChecksumMismatchException and blocks startup. Any
-- later correction has to arrive as 017, not as an edit here.
--
-- Nothing already confirmed is touched. There is no INSERT, UPDATE, or DELETE against either
-- timeline table anywhere in this script; the only reference to one is the created_trip_leg_id
-- foreign key below, which FR-040 depends on (FR-043, FR-048).

-- 1. What the draft proposes to become. 'item' is the default so every existing row keeps the
-- behaviour it already had, and so a booking that is not transportation is unaffected.
ALTER TABLE parsed_item_drafts
    ADD COLUMN IF NOT EXISTS proposed_outcome text NOT NULL DEFAULT 'item';

-- 2. The route. Null on an item draft, and on a transport draft the email did not fully state.
-- For a car rental, origin is the pickup counter and destination is the return counter (FR-035).
ALTER TABLE parsed_item_drafts
    ADD COLUMN IF NOT EXISTS origin text NULL,
    ADD COLUMN IF NOT EXISTS destination text NULL,
    ADD COLUMN IF NOT EXISTS transportation_mode text NULL;

-- 3. Booking price. travel_cost matches trip_legs.travel_cost exactly so confirming copies it
-- across without conversion. The currency is recorded only so the review screen can label the
-- amount; it is never written to a leg, because a leg has no currency of its own (FR-010).
ALTER TABLE parsed_item_drafts
    ADD COLUMN IF NOT EXISTS travel_cost numeric(12,2) NULL,
    ADD COLUMN IF NOT EXISTS travel_cost_currency text NULL;

-- 4. Traceability, mirroring tracked_item_id from 013. ON DELETE SET NULL rather than CASCADE:
-- deleting the leg should not erase the record that the email arrived and was acted on (FR-038,
-- FR-040).
ALTER TABLE parsed_item_drafts
    ADD COLUMN IF NOT EXISTS created_trip_leg_id uuid NULL
        REFERENCES trip_legs (trip_leg_id) ON DELETE SET NULL;

-- 5. Whether this draft has been through transport recognition (FR-045).
--
-- The column is added with DEFAULT 'pending' so every row that predates this feature is marked
-- legacy in the same statement that creates the column, then the default is switched to
-- 'current' so every draft recognized from here on is already up to date.
--
-- Deliberately not a backfill UPDATE. A guarded UPDATE keyed on review_status would also match
-- drafts created after this feature shipped, since a fresh draft is pending_review too, and each
-- wrongly flagged draft costs a needless recognition call. Setting the value through the column
-- default confines it to rows that existed when the column was created, which is exactly the
-- population FR-045 is about.
ALTER TABLE parsed_item_drafts
    ADD COLUMN IF NOT EXISTS transport_recognition_state text NOT NULL DEFAULT 'pending';

ALTER TABLE parsed_item_drafts
    ALTER COLUMN transport_recognition_state SET DEFAULT 'current';

-- 6. Which fields the traveler has supplied by hand, so re-recognition cannot overwrite them.
-- An empty array means nothing has been edited. A field named here is left alone even when it is
-- null, which is what makes a deliberately cleared value stay cleared (FR-046).
ALTER TABLE parsed_item_drafts
    ADD COLUMN IF NOT EXISTS traveler_edited_fields text[] NOT NULL DEFAULT '{}';

-- 7. Row shape. The mode list is duplicated from trip_legs rather than shared, exactly as 014
-- duplicates it: TransportationModes.All is the single authority in code, and these checks are
-- the database's own guard rail.
ALTER TABLE parsed_item_drafts DROP CONSTRAINT IF EXISTS parsed_item_drafts_proposed_outcome_chk;
ALTER TABLE parsed_item_drafts
    ADD CONSTRAINT parsed_item_drafts_proposed_outcome_chk CHECK (proposed_outcome IN ('leg', 'item'));

ALTER TABLE parsed_item_drafts DROP CONSTRAINT IF EXISTS parsed_item_drafts_transportation_mode_chk;
ALTER TABLE parsed_item_drafts
    ADD CONSTRAINT parsed_item_drafts_transportation_mode_chk CHECK (
        transportation_mode IS NULL
        OR transportation_mode IN ('flight', 'train', 'bus', 'boat', 'car'));

ALTER TABLE parsed_item_drafts DROP CONSTRAINT IF EXISTS parsed_item_drafts_travel_cost_chk;
ALTER TABLE parsed_item_drafts
    ADD CONSTRAINT parsed_item_drafts_travel_cost_chk CHECK (travel_cost IS NULL OR travel_cost >= 0);

ALTER TABLE parsed_item_drafts DROP CONSTRAINT IF EXISTS parsed_item_drafts_recognition_state_chk;
ALTER TABLE parsed_item_drafts
    ADD CONSTRAINT parsed_item_drafts_recognition_state_chk CHECK (
        transport_recognition_state IN ('current', 'pending', 'unavailable'));
