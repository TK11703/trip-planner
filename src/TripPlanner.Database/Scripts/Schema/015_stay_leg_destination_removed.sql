-- 015: A stay no longer carries a destination.
--
-- Feature 025 asked a stay for both a title and a destination, which made the traveler name the
-- same place twice: "Oahu, Hawaii" as the leg and "Oahu, Hawaii" again as where it is. The
-- addresses that actually matter belong to the items inside the leg, so the leg-level
-- destination only ever restated the title. It is now travel-only, matching origin.

-- Clear what legacy stays already stored, so the value stops surfacing on the timeline and the
-- printable trip for legs the traveler may never reopen.
UPDATE trip_legs
SET destination = NULL
WHERE leg_kind = 'stay' AND destination IS NOT NULL;

ALTER TABLE trip_legs DROP CONSTRAINT IF EXISTS trip_legs_stay_shape_chk;
ALTER TABLE trip_legs
    ADD CONSTRAINT trip_legs_stay_shape_chk CHECK (
        leg_kind <> 'stay'
        OR (origin IS NULL AND destination IS NULL AND travel_cost IS NULL AND confirmation_code IS NULL));
