INSERT INTO inbox_email_attachments (attachment_id, inbox_email_id, file_name, content_type, size_bytes, content, extracted_text)
VALUES (@AttachmentId, @InboxEmailId, @FileName, @ContentType, @SizeBytes, @Content, @ExtractedText)
RETURNING attachment_id AS AttachmentId,
          inbox_email_id AS InboxEmailId,
          file_name AS FileName,
          content_type AS ContentType,
          size_bytes AS SizeBytes,
          extracted_text AS ExtractedText,
          created_at_utc AS CreatedAtUtc;
