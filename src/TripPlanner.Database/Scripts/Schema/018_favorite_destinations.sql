CREATE TABLE favorite_destinations (
    favorite_destination_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_user_id text NOT NULL,
    name text NOT NULL CHECK (length(btrim(name)) > 0),
    address text NOT NULL CHECK (length(btrim(address)) > 0),
    city text,
    country text,
    notes text,
    source text,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX favorite_destinations_owner_sort_idx
    ON favorite_destinations (
        owner_user_id,
        (CASE WHEN NULLIF(btrim(country), '') IS NULL THEN 1 ELSE 0 END),
        lower(btrim(country)),
        (CASE WHEN NULLIF(btrim(city), '') IS NULL THEN 1 ELSE 0 END),
        lower(btrim(city)),
        lower(btrim(name)),
        favorite_destination_id
    );

CREATE INDEX favorite_destinations_owner_duplicate_idx
    ON favorite_destinations (owner_user_id, lower(btrim(name)), lower(btrim(address)));