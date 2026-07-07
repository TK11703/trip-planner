UPDATE email_delivery_requests
SET status = @Status,
    attempt_count = attempt_count + 1,
    last_attempt_at_utc = @NowUtc,
    sent_at_utc = CASE WHEN @Status = 'sent' THEN @NowUtc ELSE sent_at_utc END,
    failure_reason = @FailureReason
WHERE notification_id = @NotificationId;

UPDATE notifications
SET email_delivery_status = @Status,
    email_sent_at_utc = CASE WHEN @Status = 'sent' THEN @NowUtc ELSE email_sent_at_utc END,
    email_failure_reason = @FailureReason
WHERE notification_id = @NotificationId;
