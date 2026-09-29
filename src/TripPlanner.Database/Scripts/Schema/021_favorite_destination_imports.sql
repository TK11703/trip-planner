CREATE TABLE favorite_destination_imports (
    import_id uuid PRIMARY KEY,
    owner_user_id text NOT NULL,
    file_name text NOT NULL,
    status text NOT NULL
        CHECK (status IN ('Queued', 'Processing', 'NeedsReview', 'Completed', 'Failed')),
    total_rows integer NOT NULL CHECK (total_rows >= 0),
    imported_count integer NOT NULL DEFAULT 0,
    error_message text,
    attempt_count integer NOT NULL DEFAULT 0,
    lease_expires_at_utc timestamptz,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz NOT NULL
);

CREATE INDEX favorite_destination_imports_owner_idx
    ON favorite_destination_imports (owner_user_id, created_at_utc DESC);

CREATE INDEX favorite_destination_imports_pending_idx
    ON favorite_destination_imports (created_at_utc)
    WHERE status IN ('Queued', 'Processing');

CREATE TABLE favorite_destination_import_rows (
    import_id uuid NOT NULL REFERENCES favorite_destination_imports (import_id) ON DELETE CASCADE,
    row_number integer NOT NULL,
    name text NOT NULL,
    submitted_address text NOT NULL,
    notes text,
    status text NOT NULL DEFAULT 'Pending'
        CHECK (status IN ('Pending', 'Resolved', 'Ambiguous')),
    uses_resolved_address boolean NOT NULL DEFAULT false,
    address text,
    city text,
    country text,
    latitude double precision,
    longitude double precision,
    candidates jsonb,
    is_possible_duplicate boolean NOT NULL DEFAULT false,
    PRIMARY KEY (import_id, row_number)
);
