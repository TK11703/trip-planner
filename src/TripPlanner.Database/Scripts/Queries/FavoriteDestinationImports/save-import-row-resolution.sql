WITH updated_row AS (
    UPDATE favorite_destination_import_rows
    SET status = @Status,
        uses_resolved_address = @UsesResolvedAddress,
        address = @Address,
        city = @City,
        country = @Country,
        latitude = @Latitude,
        longitude = @Longitude,
        candidates = @CandidatesJson::jsonb
    WHERE import_id = @ImportId
      AND row_number = @RowNumber
    RETURNING import_id
)
UPDATE favorite_destination_imports
SET lease_expires_at_utc = @LeaseExpiresAtUtc,
    updated_at_utc = @NowUtc
WHERE import_id = @ImportId
  AND status = 'Processing';
