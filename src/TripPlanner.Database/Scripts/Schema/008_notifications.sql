-- Feature 011: User Notifications.
-- Recipient-owned notifications with optional trip targeting, actionable completion metadata,
-- soft deletion, category/channel preferences, and an email delivery outbox.

CREATE TABLE IF NOT EXISTS notifications (
    notification_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    recipient_user_id text NOT NULL,
    category text NOT NULL,
    kind text NOT NULL,
    target_type text NOT NULL,
    related_trip_id uuid NULL REFERENCES trips(trip_id) ON DELETE SET NULL,
    title text NOT NULL,
    message text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    read_at_utc timestamptz NULL,
    deleted_at_utc timestamptz NULL,
    action_status text NOT NULL DEFAULT 'not_applicable',
    completed_at_utc timestamptz NULL,
    completed_by_user_id text NULL,
    completed_by_display_name text NULL,
    source_event_key text NOT NULL,
    in_app_delivered_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    email_delivery_status text NOT NULL DEFAULT 'not_requested',
    email_requested_at_utc timestamptz NULL,
    email_sent_at_utc timestamptz NULL,
    email_failure_reason text NULL,
    CONSTRAINT notifications_kind_chk CHECK (kind IN ('awareness','actionable')),
    CONSTRAINT notifications_target_type_chk CHECK (target_type IN ('person','trip')),
    CONSTRAINT notifications_action_status_chk CHECK (action_status IN ('not_applicable','pending','completed')),
    CONSTRAINT notifications_email_status_chk CHECK (email_delivery_status IN ('not_requested','pending','sent','failed','suppressed')),
    CONSTRAINT notifications_recipient_event_uq UNIQUE (recipient_user_id, source_event_key)
);

-- Newest-first recipient listing and unread counts, excluding soft-deleted rows.
CREATE INDEX IF NOT EXISTS notifications_recipient_created_idx
    ON notifications (recipient_user_id, created_at_utc DESC)
    WHERE deleted_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS notifications_recipient_unread_idx
    ON notifications (recipient_user_id)
    WHERE deleted_at_utc IS NULL AND read_at_utc IS NULL;

CREATE TABLE IF NOT EXISTS notification_preferences (
    user_id text NOT NULL,
    category text NOT NULL,
    in_app_enabled boolean NOT NULL DEFAULT true,
    email_enabled boolean NOT NULL DEFAULT true,
    updated_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT notification_preferences_pk PRIMARY KEY (user_id, category)
);

CREATE TABLE IF NOT EXISTS email_delivery_requests (
    email_delivery_request_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    notification_id uuid NOT NULL REFERENCES notifications(notification_id) ON DELETE CASCADE,
    recipient_user_id text NOT NULL,
    recipient_email text NULL,
    status text NOT NULL DEFAULT 'pending',
    attempt_count integer NOT NULL DEFAULT 0,
    last_attempt_at_utc timestamptz NULL,
    sent_at_utc timestamptz NULL,
    failure_reason text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT timezone('utc', now()),
    CONSTRAINT email_delivery_requests_status_chk CHECK (status IN ('pending','sent','failed','suppressed'))
);

CREATE INDEX IF NOT EXISTS email_delivery_requests_notification_idx
    ON email_delivery_requests (notification_id);
