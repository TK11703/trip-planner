UPDATE favorite_destination_imports AS i
SET status = 'Processing',
    attempt_count = i.attempt_count + 1,
    lease_expires_at_utc = @LeaseExpiresAtUtc,
    updated_at_utc = @NowUtc
WHERE i.import_id = (
    SELECT candidate.import_id
    FROM favorite_destination_imports candidate
    WHERE candidate.status = 'Queued'
       OR (candidate.status = 'Processing' AND candidate.lease_expires_at_utc < @NowUtc)
    ORDER BY candidate.created_at_utc
    LIMIT 1
    FOR UPDATE SKIP LOCKED
)
RETURNING
    i.import_id AS "ImportId",
    i.owner_user_id AS "OwnerUserId",
    i.file_name AS "FileName",
    i.status AS "Status",
    i.total_rows AS "TotalRows",
    0 AS "ProcessedRows",
    i.imported_count AS "ImportedCount",
    i.error_message AS "ErrorMessage",
    i.attempt_count AS "AttemptCount",
    i.created_at_utc AS "CreatedAtUtc",
    i.updated_at_utc AS "UpdatedAtUtc";
