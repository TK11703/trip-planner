INSERT INTO inbox_emails (inbox_email_id, user_id, message_id, sender, recipient, subject, body_text, body_html, received_at, dedupe_hash, parse_status)
VALUES (@InboxEmailId, @UserId, @MessageId, @Sender, @Recipient, @Subject, @BodyText, @BodyHtml, @ReceivedAt, @DedupeHash, @ParseStatus)
ON CONFLICT (user_id, dedupe_hash) DO NOTHING
RETURNING inbox_email_id AS InboxEmailId,
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
          created_at_utc AS CreatedAtUtc;
