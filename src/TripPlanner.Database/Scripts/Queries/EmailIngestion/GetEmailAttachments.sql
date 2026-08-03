SELECT attachment_id AS AttachmentId,
       inbox_email_id AS InboxEmailId,
       file_name AS FileName,
       content_type AS ContentType,
       size_bytes AS SizeBytes,
       extracted_text AS ExtractedText,
       created_at_utc AS CreatedAtUtc
FROM inbox_email_attachments
WHERE inbox_email_id = @InboxEmailId
ORDER BY created_at_utc, file_name;
