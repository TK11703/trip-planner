WITH inserted_import AS (
    INSERT INTO favorite_destination_imports (
        import_id,
        owner_user_id,
        file_name,
        status,
        total_rows,
        created_at_utc,
        updated_at_utc
    )
    VALUES (
        @ImportId,
        @OwnerUserId,
        @FileName,
        'Queued',
        cardinality(@RowNumbers::integer[]),
        @NowUtc,
        @NowUtc
    )
    RETURNING import_id
)
INSERT INTO favorite_destination_import_rows (
    import_id,
    row_number,
    name,
    submitted_address,
    notes
)
SELECT
    inserted_import.import_id,
    row.row_number,
    row.name,
    row.submitted_address,
    row.notes
FROM inserted_import
CROSS JOIN unnest(
    @RowNumbers::integer[],
    @Names::text[],
    @SubmittedAddresses::text[],
    @NotesValues::text[]
) AS row(row_number, name, submitted_address, notes);
