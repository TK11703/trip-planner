SELECT
    i.import_id AS "ImportId",
    i.owner_user_id AS "OwnerUserId",
    i.file_name AS "FileName",
    i.status AS "Status",
    i.total_rows AS "TotalRows",
    CASE
        WHEN i.status = 'Completed' THEN i.total_rows
        ELSE (
            SELECT count(*)::integer
            FROM favorite_destination_import_rows r
            WHERE r.import_id = i.import_id
              AND r.status <> 'Pending'
        )
    END AS "ProcessedRows",
    i.imported_count AS "ImportedCount",
    i.error_message AS "ErrorMessage",
    i.attempt_count AS "AttemptCount",
    i.created_at_utc AS "CreatedAtUtc",
    i.updated_at_utc AS "UpdatedAtUtc"
FROM favorite_destination_imports i
WHERE i.owner_user_id = @OwnerUserId
  AND (@ImportId::uuid IS NULL OR i.import_id = @ImportId::uuid)
  AND (NOT @OpenOnly OR i.status <> 'Completed')
ORDER BY i.created_at_utc DESC, i.import_id;
