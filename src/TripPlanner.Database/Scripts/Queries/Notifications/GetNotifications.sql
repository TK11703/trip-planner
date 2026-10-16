SELECT n.notification_id AS NotificationId,
       n.recipient_user_id AS RecipientUserId,
       n.category AS Category,
       n.kind AS Kind,
       n.target_type AS TargetType,
       n.related_trip_id AS RelatedTripId,
       t.name AS RelatedTripName,
       n.title AS Title,
       n.message AS Message,
       n.created_at_utc AS CreatedAtUtc,
       n.read_at_utc AS ReadAtUtc,
       n.action_status AS ActionStatus,
       n.completed_at_utc AS CompletedAtUtc,
       n.completed_by_user_id AS CompletedByUserId,
       n.completed_by_display_name AS CompletedByDisplayName,
       n.email_delivery_status AS EmailDeliveryStatus,
       e.subject AS RelatedEmailSubject
FROM notifications n
LEFT JOIN trips t ON t.trip_id = n.related_trip_id
LEFT JOIN inbox_emails e ON n.category = 'ParsedEmail'
    AND e.user_id = n.recipient_user_id
    AND n.source_event_key = 'email-parsed:' || e.inbox_email_id::text
WHERE n.recipient_user_id = @RecipientUserId
  AND n.deleted_at_utc IS NULL
ORDER BY n.created_at_utc DESC
LIMIT @Limit;
