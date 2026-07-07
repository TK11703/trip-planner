UPDATE notifications
SET read_at_utc = @NowUtc
WHERE recipient_user_id = @RecipientUserId
  AND deleted_at_utc IS NULL
  AND read_at_utc IS NULL;
