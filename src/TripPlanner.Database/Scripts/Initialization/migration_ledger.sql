-- Migration ledger bootstrap.
-- Applied before any schema script so the initializer can tell which migrations have
-- already run. Must stay idempotent: it executes on every start.

CREATE TABLE IF NOT EXISTS schema_migrations (
    migration_id            text        NOT NULL PRIMARY KEY,
    checksum                text        NOT NULL,
    applied_at_utc          timestamptz NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    release_id              text        NOT NULL,
    duration_milliseconds   bigint      NOT NULL CHECK (duration_milliseconds >= 0)
);

COMMENT ON TABLE schema_migrations IS
    'One row per applied schema script. A recorded id with a different checksum means an already-applied script was edited, which blocks startup.';
