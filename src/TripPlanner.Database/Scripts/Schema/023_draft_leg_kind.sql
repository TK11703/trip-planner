ALTER TABLE parsed_item_drafts
    ADD COLUMN leg_kind text NULL
    CONSTRAINT parsed_item_drafts_leg_kind_chk CHECK (leg_kind IN ('travel', 'stay'));
