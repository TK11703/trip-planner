ALTER TABLE users
    ADD COLUMN item_type_colors jsonb NOT NULL DEFAULT '{}'::jsonb
    CONSTRAINT ck_users_item_type_colors_object CHECK (jsonb_typeof(item_type_colors) = 'object');
