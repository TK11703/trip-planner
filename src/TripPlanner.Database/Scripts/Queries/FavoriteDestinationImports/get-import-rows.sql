SELECT
    r.row_number AS "RowNumber",
    r.name AS "Name",
    r.submitted_address AS "SubmittedAddress",
    r.notes AS "Notes",
    r.status AS "Status",
    r.uses_resolved_address AS "UsesResolvedAddress",
    r.address AS "Address",
    r.city AS "City",
    r.country AS "Country",
    r.latitude AS "Latitude",
    r.longitude AS "Longitude",
    r.candidates::text AS "CandidatesJson",
    r.is_possible_duplicate AS "IsPossibleDuplicate"
FROM favorite_destination_import_rows r
JOIN favorite_destination_imports i ON i.import_id = r.import_id
WHERE i.owner_user_id = @OwnerUserId
  AND r.import_id = @ImportId
ORDER BY r.row_number;
