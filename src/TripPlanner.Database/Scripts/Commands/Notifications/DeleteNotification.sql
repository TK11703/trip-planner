UPDATE notifications
SET deleted_at_utc = @NowUtc
WHERE recipient_user_id = @RecipientUserId
  AND notification_id = @NotificationId
  AND deleted_at_utc IS NULL;
