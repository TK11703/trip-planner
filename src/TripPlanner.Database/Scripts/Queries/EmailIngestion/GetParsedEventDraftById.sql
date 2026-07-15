SELECT d.parsed_event_draft_id AS ParsedEventDraftId,
       d.inbox_email_id AS InboxEmailId,
       d.user_id AS UserId,
       d.trip_id AS TripId,
       d.trip_leg_id AS TripLegId,
       d.event_type AS EventType,
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
FROM parsed_event_drafts d
WHERE d.parsed_event_draft_id = @ParsedEventDraftId
  AND d.user_id = @UserId;
