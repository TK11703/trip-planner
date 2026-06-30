-- Base schema: shared extensions and conventions. Applied in file-name order during dev/test init.
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

CREATE OR REPLACE FUNCTION set_updated_at_utc()
RETURNS trigger AS $$
BEGIN
    NEW.updated_at_utc := timezone('utc', now());
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
