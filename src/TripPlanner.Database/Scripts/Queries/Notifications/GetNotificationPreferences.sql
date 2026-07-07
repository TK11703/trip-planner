SELECT user_id AS UserId,
       category AS Category,
       in_app_enabled AS InAppEnabled,
       email_enabled AS EmailEnabled,
       updated_at_utc AS UpdatedAtUtc
FROM notification_preferences
WHERE user_id = @UserId;
