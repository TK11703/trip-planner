SELECT d.parsed_item_draft_id AS ParsedItemDraftId,
       d.inbox_email_id AS InboxEmailId,
       d.user_id AS UserId,
       d.trip_id AS TripId,
       d.trip_leg_id AS TripLegId,
       d.item_type AS ItemType,
       d.title AS Title,
       d.location AS Location,
       d.start_local AS StartLocal,
       d.start_timezone_id AS StartTimeZoneId,
       d.end_local AS EndLocal,
       d.end_timezone_id AS EndTimeZoneId,
       d.confirmation_code AS ConfirmationCode,
       d.notes AS Notes,
       d.confidence AS Confidence,
       d.review_status AS ReviewStatus,
       d.created_at_utc AS CreatedAtUtc
FROM parsed_item_drafts d
WHERE d.parsed_item_draft_id = @ParsedItemDraftId
  AND d.user_id = @UserId;
