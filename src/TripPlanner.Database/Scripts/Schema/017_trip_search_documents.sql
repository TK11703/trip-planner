CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS trip_search_documents (
    search_document_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    trip_id uuid NOT NULL REFERENCES trips(trip_id) ON DELETE CASCADE,
    owner_user_id text NOT NULL,
    source_kind text NOT NULL,
    source_id uuid NOT NULL,
    content_hash text NOT NULL,
    source_updated_at_utc timestamptz NOT NULL,
    embedding vector NOT NULL,
    embedding_model text NOT NULL,
    embedding_dimensions integer NOT NULL,
    embedded_at_utc timestamptz NOT NULL,
    CONSTRAINT trip_search_documents_source_kind_chk
        CHECK (source_kind IN ('trip', 'leg', 'tracked_item')),
    CONSTRAINT trip_search_documents_dimensions_chk
        CHECK (embedding_dimensions > 0 AND vector_dims(embedding) = embedding_dimensions),
    CONSTRAINT trip_search_documents_trip_source_uq
        UNIQUE (trip_id, source_kind, source_id)
);

CREATE INDEX IF NOT EXISTS trip_search_documents_trip_idx
    ON trip_search_documents (trip_id, source_kind, source_id);

CREATE INDEX IF NOT EXISTS trip_search_documents_owner_updated_idx
    ON trip_search_documents (owner_user_id, source_updated_at_utc);