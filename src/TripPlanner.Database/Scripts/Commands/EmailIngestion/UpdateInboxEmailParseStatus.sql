UPDATE inbox_emails
SET parse_status = @ParseStatus
WHERE inbox_email_id = @InboxEmailId
  AND user_id = @UserId;
