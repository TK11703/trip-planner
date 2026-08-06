-- 014: Persist what a trip leg actually is, and stop items from landing where the traveler
-- cannot choose their own stops.
--
-- Feature 025. Until now "travel" versus "stay" was inferred in the UI from whether origin
-- happened to be filled in, so nothing could depend on it. Both the classification and the
-- transportation mode are now stored, and Car is the one travel mode that still accepts items.
--
-- Written idempotently (ADD COLUMN IF NOT EXISTS / DROP CONSTRAINT IF EXISTS) because
-- DatabaseInitializer re-runs every schema script on every application start and there is no
-- migration tracking table.

-- 1. Additive columns. All nullable at first so the backfill below can classify existing rows.
ALTER TABLE trip_legs
    ADD COLUMN IF NOT EXISTS leg_kind text NULL,
    ADD COLUMN IF NOT EXISTS transportation_mode text NULL,
    ADD COLUMN IF NOT EXISTS travel_cost numeric(12,2) NULL,
    ADD COLUMN IF NOT EXISTS confirmation_code text NULL;

-- 2. A whitespace-only origin never meant "this leg goes somewhere", so it must not be read as
-- travel by the backfill.
UPDATE trip_legs
SET origin = NULL
WHERE origin IS NOT NULL AND btrim(origin) = '';

-- 3. Backfill. An origin-bearing leg becomes travel by car: car is the only mode that preserves
-- any items already assigned to it, so no legacy row needs an exception.
UPDATE trip_legs
SET leg_kind = 'travel',
    transportation_mode = COALESCE(transportation_mode, 'car')
WHERE leg_kind IS NULL AND origin IS NOT NULL;

UPDATE trip_legs
SET leg_kind = 'stay'
WHERE leg_kind IS NULL;

ALTER TABLE trip_legs
    ALTER COLUMN leg_kind SET DEFAULT 'stay',
    ALTER COLUMN leg_kind SET NOT NULL;

-- 4. Row shape. A stay carries no travel-only data; a travel leg has a mode and a real origin.
ALTER TABLE trip_legs DROP CONSTRAINT IF EXISTS trip_legs_leg_kind_chk;
ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_leg_kind_chk CHECK (leg_kind IN ('stay', 'travel'));

ALTER TABLE trip_legs DROP CONSTRAINT IF EXISTS trip_legs_transportation_mode_chk;
ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_transportation_mode_chk CHECK (
        (leg_kind = 'travel' AND transportation_mode IN ('flight', 'train', 'bus', 'boat', 'car'))
        OR (leg_kind = 'stay' AND transportation_mode IS NULL));

ALTER TABLE trip_legs DROP CONSTRAINT IF EXISTS trip_legs_stay_shape_chk;
ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_stay_shape_chk CHECK (
        leg_kind <> 'stay'
        OR (origin IS NULL AND travel_cost IS NULL AND confirmation_code IS NULL));

ALTER TABLE trip_legs DROP CONSTRAINT IF EXISTS trip_legs_travel_origin_chk;
ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_travel_origin_chk CHECK (
        leg_kind <> 'travel' OR (origin IS NOT NULL AND btrim(origin) <> ''));

-- 5. Optional booking details, on every travel mode. NULL means "not booked yet"; a supplied
-- value has to be usable. numeric(12,2) already bounds the scale.
ALTER TABLE trip_legs DROP CONSTRAINT IF EXISTS trip_legs_travel_cost_chk;
ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_travel_cost_chk CHECK (travel_cost IS NULL OR travel_cost >= 0);

ALTER TABLE trip_legs DROP CONSTRAINT IF EXISTS trip_legs_confirmation_code_chk;
ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_confirmation_code_chk CHECK (
        confirmation_code IS NULL
        OR (btrim(confirmation_code) <> '' AND length(confirmation_code) <= 255));

-- 6. Cross-table invariant, half one: an item may not point at a restricted leg.
--
-- FOR SHARE is what makes this race-safe rather than merely correct in a quiet system. It locks
-- the leg row for the life of the inserting transaction, so a concurrent switch to a restricted
-- mode must wait, then observe this item and fail in the trigger below.
CREATE OR REPLACE FUNCTION trip_legs_reject_item_on_restricted_leg() RETURNS trigger AS $fn$
DECLARE
    v_kind text;
    v_mode text;
BEGIN
    IF NEW.trip_leg_id IS NULL THEN
        RETURN NEW;
    END IF;

    SELECT leg_kind, transportation_mode
    INTO v_kind, v_mode
    FROM trip_legs
    WHERE trip_leg_id = NEW.trip_leg_id
    FOR SHARE;

    IF v_kind = 'travel' AND v_mode IS DISTINCT FROM 'car' THEN
        RAISE EXCEPTION 'Items cannot be assigned to a flight, train, bus, or boat leg.'
            USING ERRCODE = 'check_violation', CONSTRAINT = 'trip_legs_item_eligibility';
    END IF;

    RETURN NEW;
END;
$fn$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tracked_items_leg_eligibility_trg ON tracked_items;
CREATE TRIGGER tracked_items_leg_eligibility_trg
    BEFORE INSERT OR UPDATE OF trip_leg_id ON tracked_items
    FOR EACH ROW
    EXECUTE FUNCTION trip_legs_reject_item_on_restricted_leg();

-- 7. Cross-table invariant, half two: a leg may not become restricted while it still holds items.
-- Together with the FOR SHARE above, one of the two conflicting transactions always loses.
CREATE OR REPLACE FUNCTION trip_legs_reject_restricted_with_items() RETURNS trigger AS $fn$
BEGIN
    IF NEW.leg_kind = 'travel'
       AND NEW.transportation_mode IS DISTINCT FROM 'car'
       AND EXISTS (SELECT 1 FROM tracked_items WHERE trip_leg_id = NEW.trip_leg_id) THEN
        RAISE EXCEPTION 'This trip leg still has items, so it cannot become a flight, train, bus, or boat leg.'
            USING ERRCODE = 'check_violation', CONSTRAINT = 'trip_legs_item_eligibility';
    END IF;

    RETURN NEW;
END;
$fn$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trip_legs_item_eligibility_trg ON trip_legs;
CREATE TRIGGER trip_legs_item_eligibility_trg
    BEFORE UPDATE ON trip_legs
    FOR EACH ROW
    EXECUTE FUNCTION trip_legs_reject_restricted_with_items();
