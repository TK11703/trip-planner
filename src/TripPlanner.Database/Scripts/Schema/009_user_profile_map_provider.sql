-- Feature 019: Trip Location Maps.
-- Per-user default mapping tool for opening a single event location.
-- Existing rows adopt 'Bing', matching the previous hard-coded globe behavior.
ALTER TABLE users
    ADD COLUMN IF NOT EXISTS map_provider text NOT NULL DEFAULT 'Bing';
