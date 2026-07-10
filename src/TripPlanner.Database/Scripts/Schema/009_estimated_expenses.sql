-- 009: optional estimated cost per tracked item. Amounts are estimates, not
-- actuals. NULL means no estimate recorded (excluded from estimated totals);
-- 0.00 is an explicit zero estimate (included). Stored as NUMERIC(12,2) for exact
-- money precision with a non-negative check constraint. Existing rows stay NULL.
ALTER TABLE tracked_items
    ADD COLUMN IF NOT EXISTS estimated_cost numeric(12,2) NULL;

ALTER TABLE tracked_items
    DROP CONSTRAINT IF EXISTS tracked_items_estimated_cost_chk;

ALTER TABLE tracked_items
    ADD CONSTRAINT tracked_items_estimated_cost_chk CHECK (estimated_cost IS NULL OR estimated_cost >= 0);
