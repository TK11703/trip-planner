SELECT inbox_email_id AS InboxEmailId,
       user_id AS UserId,
       sender AS Sender,
       subject AS Subject,
       body_text AS BodyText,
       body_html AS BodyHtml,
       received_at AS ReceivedAt,
       dedupe_hash AS DedupeHash,
       parse_status AS ParseStatus,
       created_at_utc AS CreatedAtUtc
FROM inbox_emails
WHERE parse_status = 'pending'
ORDER BY created_at_utc
LIMIT @Limit;
