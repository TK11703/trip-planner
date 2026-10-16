ALTER TABLE parsed_item_drafts
    ADD COLUMN estimated_cost numeric(12,2) NULL
    CONSTRAINT parsed_item_drafts_estimated_cost_chk CHECK (estimated_cost >= 0);
