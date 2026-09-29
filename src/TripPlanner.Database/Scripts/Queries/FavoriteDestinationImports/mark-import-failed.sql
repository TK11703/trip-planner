UPDATE favorite_destination_imports
SET status = 'Failed',
    error_message = @ErrorMessage,
    lease_expires_at_utc = NULL,
    updated_at_utc = @NowUtc
WHERE import_id = @ImportId
  AND status IN ('Queued', 'Processing');
