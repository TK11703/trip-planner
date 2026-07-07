WITH inserted AS (
    INSERT INTO email_delivery_requests (
        email_delivery_request_id,
        notification_id,
        recipient_user_id,
        recipient_email,
        status,
        attempt_count,
        created_at_utc)
    VALUES (
        gen_random_uuid(),
        @NotificationId,
        @RecipientUserId,
        @RecipientEmail,
        @Status,
        0,
        @NowUtc)
    RETURNING notification_id)
UPDATE notifications
SET email_delivery_status = @Status,
    email_requested_at_utc = @NowUtc
WHERE notification_id = @NotificationId;
