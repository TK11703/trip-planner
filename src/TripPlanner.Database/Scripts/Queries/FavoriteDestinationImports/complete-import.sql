UPDATE favorite_destination_imports
SET status = 'Completed',
    imported_count = @ImportedCount,
    error_message = NULL,
    lease_expires_at_utc = NULL,
    updated_at_utc = @NowUtc
WHERE import_id = @ImportId
  AND owner_user_id = @OwnerUserId
  AND status = @ExpectedStatus;
