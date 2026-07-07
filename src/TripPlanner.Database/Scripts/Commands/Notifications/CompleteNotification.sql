UPDATE notifications
SET action_status = 'completed',
    completed_at_utc = @NowUtc,
    completed_by_user_id = @CompletedByUserId,
    completed_by_display_name = @CompletedByDisplayName,
    read_at_utc = COALESCE(read_at_utc, @NowUtc)
WHERE recipient_user_id = @RecipientUserId
  AND notification_id = @NotificationId
  AND deleted_at_utc IS NULL
  AND kind = 'actionable'
  AND action_status = 'pending'
RETURNING notification_id AS NotificationId,
          recipient_user_id AS RecipientUserId,
          category AS Category,
          kind AS Kind,
          target_type AS TargetType,
          related_trip_id AS RelatedTripId,
          NULL::text AS RelatedTripName,
          title AS Title,
          message AS Message,
          created_at_utc AS CreatedAtUtc,
          read_at_utc AS ReadAtUtc,
          action_status AS ActionStatus,
          completed_at_utc AS CompletedAtUtc,
          completed_by_user_id AS CompletedByUserId,
          completed_by_display_name AS CompletedByDisplayName,
          email_delivery_status AS EmailDeliveryStatus;
