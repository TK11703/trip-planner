-- 013: Record which timeline item each confirmed draft became.
--
-- Feature 024, FR-025: after a traveler confirms a draft, the draft should point at the
-- item it produced, so the path from a forwarded email to an itinerary entry stays
-- traceable.
--
-- ON DELETE SET NULL rather than CASCADE: deleting the item should not erase the record
-- that the email arrived and was acted on.
--
-- Written with ADD COLUMN IF NOT EXISTS because DatabaseInitializer re-runs every schema
-- script on every application start and there is no migration tracking table.

ALTER TABLE parsed_item_drafts
    ADD COLUMN IF NOT EXISTS tracked_item_id uuid NULL
        REFERENCES tracked_items (tracked_item_id) ON DELETE SET NULL;
