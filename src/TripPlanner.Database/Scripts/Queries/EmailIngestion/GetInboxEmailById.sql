SELECT inbox_email_id AS InboxEmailId,
       user_id AS UserId,
       message_id AS MessageId,
       sender AS Sender,
       recipient AS Recipient,
       subject AS Subject,
       body_text AS BodyText,
       body_html AS BodyHtml,
       received_at AS ReceivedAt,
       dedupe_hash AS DedupeHash,
       parse_status AS ParseStatus,
       created_at_utc AS CreatedAtUtc
FROM inbox_emails
WHERE inbox_email_id = @InboxEmailId
  AND user_id = @UserId;
