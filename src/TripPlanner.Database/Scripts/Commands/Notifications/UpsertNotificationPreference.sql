INSERT INTO notification_preferences (user_id, category, in_app_enabled, email_enabled, updated_at_utc)
VALUES (@UserId, @Category, @InAppEnabled, @EmailEnabled, @NowUtc)
ON CONFLICT (user_id, category)
DO UPDATE SET in_app_enabled = EXCLUDED.in_app_enabled,
              email_enabled = EXCLUDED.email_enabled,
              updated_at_utc = EXCLUDED.updated_at_utc
RETURNING user_id AS UserId,
          category AS Category,
          in_app_enabled AS InAppEnabled,
          email_enabled AS EmailEnabled,
          updated_at_utc AS UpdatedAtUtc;
