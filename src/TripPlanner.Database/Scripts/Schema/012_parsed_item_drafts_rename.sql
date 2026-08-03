-- 012: Reconcile the legacy parsed_event_drafts table into parsed_item_drafts.
--
-- Script 010 now creates parsed_item_drafts directly, so on a database that was
-- initialized before feature 023 both tables exist after a restart: the legacy
-- table holding the real rows, and a freshly created empty parsed_item_drafts.
-- A plain ALTER TABLE ... RENAME cannot be used because the target name is
-- already taken by that empty table. Instead, copy the legacy rows across and
-- drop the legacy table.
--
-- Guarded so it is a no-op on a fresh database and on every restart after the
-- first, which matters because DatabaseInitializer re-runs every schema script
-- on every application start (there is no migration tracking table).

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = current_schema()
          AND table_name = 'parsed_event_drafts'
    ) THEN
        INSERT INTO parsed_item_drafts (
            parsed_item_draft_id, inbox_email_id, user_id, trip_id, trip_leg_id,
            item_type, title, location, start_local, start_timezone_id,
            end_local, end_timezone_id, confirmation_code, notes, confidence,
            review_status, created_at_utc
        )
        SELECT
            parsed_event_draft_id, inbox_email_id, user_id, trip_id, trip_leg_id,
            event_type, title, location, start_local, start_timezone_id,
            end_local, end_timezone_id, confirmation_code, notes, confidence,
            review_status, created_at_utc
        FROM parsed_event_drafts
        ON CONFLICT (parsed_item_draft_id) DO NOTHING;

        DROP TABLE parsed_event_drafts;
    END IF;
END
$$;
