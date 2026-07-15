-- Feature 021: Email Event Ingestion.
-- Raw inbox emails received from ACS/Event Grid, a deduplication hash,
-- and parsed event drafts awaiting user review before promotion to TrackedItems.

CREATE TABLE IF NOT EXISTS inbox_emails (
    inbox_email_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id text NOT NULL,
    sender text NOT NULL,
    subject text NOT NULL,
    body_text text NOT NULL DEFAULT '',
    body_html text NULL,
    received_at timestamptz NOT NULL,
    dedupe_hash text NOT NULL,
    parse_status text NOT NULL DEFAULT 'pending',
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT inbox_emails_parse_status_chk CHECK (parse_status IN ('pending','parsed','failed','unsupported')),
    CONSTRAINT inbox_emails_dedupe_uq UNIQUE (user_id, dedupe_hash)
);

-- Fast lookup of emails by user, newest first, and by status for background processing.
CREATE INDEX IF NOT EXISTS inbox_emails_user_created_idx
    ON inbox_emails (user_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS inbox_emails_pending_idx
    ON inbox_emails (parse_status)
    WHERE parse_status = 'pending';

CREATE TABLE IF NOT EXISTS parsed_event_drafts (
    parsed_event_draft_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    inbox_email_id uuid NOT NULL REFERENCES inbox_emails(inbox_email_id) ON DELETE CASCADE,
    user_id text NOT NULL,
    trip_id uuid NULL REFERENCES trips(trip_id) ON DELETE SET NULL,
    trip_leg_id uuid NULL,
    event_type text NULL,
    title text NULL,
    location text NULL,
    start_local timestamp NULL,
    start_timezone_id text NULL,
    end_local timestamp NULL,
    end_timezone_id text NULL,
    confirmation_code text NULL,
    notes text NULL,
    confidence double precision NOT NULL DEFAULT 0,
    review_status text NOT NULL DEFAULT 'pending_review',
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT parsed_event_drafts_review_status_chk CHECK (review_status IN ('pending_review','confirmed','discarded'))
);

-- Pending drafts by user for the review queue.
CREATE INDEX IF NOT EXISTS parsed_event_drafts_user_pending_idx
    ON parsed_event_drafts (user_id, created_at_utc DESC)
    WHERE review_status = 'pending_review';
