SELECT inbox_email_id AS InboxEmailId,
       user_id AS UserId,
       sender AS Sender,
       subject AS Subject,
       received_at AS ReceivedAt,
       parse_status AS ParseStatus,
       created_at_utc AS CreatedAtUtc
FROM inbox_emails
WHERE user_id = @UserId
ORDER BY created_at_utc DESC
LIMIT @Limit;
