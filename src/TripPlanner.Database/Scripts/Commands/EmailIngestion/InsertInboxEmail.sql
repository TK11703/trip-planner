INSERT INTO inbox_emails (inbox_email_id, user_id, sender, subject, body_text, body_html, received_at, dedupe_hash, parse_status)
VALUES (@InboxEmailId, @UserId, @Sender, @Subject, @BodyText, @BodyHtml, @ReceivedAt, @DedupeHash, 'pending')
ON CONFLICT (user_id, dedupe_hash) DO NOTHING
RETURNING inbox_email_id AS InboxEmailId,
          user_id AS UserId,
          sender AS Sender,
          subject AS Subject,
          received_at AS ReceivedAt,
          parse_status AS ParseStatus,
          created_at_utc AS CreatedAtUtc;
