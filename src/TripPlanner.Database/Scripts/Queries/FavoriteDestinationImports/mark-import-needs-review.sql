WITH flagged_rows AS (
    UPDATE favorite_destination_import_rows
    SET is_possible_duplicate = (row_number = ANY(@DuplicateRowNumbers::integer[]))
    WHERE import_id = @ImportId
    RETURNING import_id
)
UPDATE favorite_destination_imports
SET status = 'NeedsReview',
    lease_expires_at_utc = NULL,
    updated_at_utc = @NowUtc
WHERE import_id = @ImportId
  AND status = 'Processing';
