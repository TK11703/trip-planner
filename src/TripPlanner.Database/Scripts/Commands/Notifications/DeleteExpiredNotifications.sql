-- Retention cleanup: permanently remove notifications older than the retention window.
-- Intended to be run by a scheduled maintenance job. @CutoffUtc is the oldest created_at_utc to keep.
DELETE FROM notifications
WHERE created_at_utc < @CutoffUtc;
