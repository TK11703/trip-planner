-- Feature 022: Relayed Email Content Ingestion.
-- The API no longer monitors a mailbox. An external automation relay pushes each message to
-- POST /api/email-ingestion/messages and the message is processed synchronously inside that
-- request, so no row is ever left awaiting a later pass. Every statement is idempotent.

-- Originating message identifier (drives duplicate detection) and the monitored mailbox the
-- relay picked the message up from (diagnostics only).
ALTER TABLE inbox_emails
    ADD COLUMN IF NOT EXISTS message_id text NULL,
    ADD COLUMN IF NOT EXISTS recipient text NULL;

-- The polling index existed only to feed the deleted background service.
DROP INDEX IF EXISTS inbox_emails_pending_idx;

-- No deferred state remains. Migrate any survivor before tightening the constraint.
UPDATE inbox_emails SET parse_status = 'failed' WHERE parse_status = 'pending';

ALTER TABLE inbox_emails DROP CONSTRAINT IF EXISTS inbox_emails_parse_status_chk;
ALTER TABLE inbox_emails
    ADD CONSTRAINT inbox_emails_parse_status_chk
    CHECK (parse_status IN ('parsed','failed','unsupported'));

-- 'pending' is no longer a legal default.
ALTER TABLE inbox_emails ALTER COLUMN parse_status DROP DEFAULT;

-- Files delivered with a relayed message. Bytes are retained for the owning traveler;
-- extracted_text is populated only for text-based content types.
CREATE TABLE IF NOT EXISTS inbox_email_attachments (
    attachment_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    inbox_email_id uuid NOT NULL REFERENCES inbox_emails(inbox_email_id) ON DELETE CASCADE,
    file_name text NOT NULL,
    content_type text NOT NULL,
    size_bytes integer NOT NULL,
    content bytea NOT NULL,
    extracted_text text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT inbox_email_attachments_size_chk CHECK (size_bytes >= 0)
);

CREATE INDEX IF NOT EXISTS inbox_email_attachments_email_idx
    ON inbox_email_attachments (inbox_email_id);
