UPDATE parsed_event_drafts
SET trip_id = @TripId,
    trip_leg_id = @TripLegId,
    event_type = @EventType,
    title = @Title,
    location = @Location,
    start_local = @StartLocal,
    start_timezone_id = @StartTimeZoneId,
    end_local = @EndLocal,
    end_timezone_id = @EndTimeZoneId,
    confirmation_code = @ConfirmationCode,
    notes = @Notes
WHERE parsed_event_draft_id = @ParsedEventDraftId
  AND user_id = @UserId
  AND review_status = 'pending_review'
RETURNING parsed_event_draft_id AS ParsedEventDraftId,
          inbox_email_id AS InboxEmailId,
          user_id AS UserId,
          trip_id AS TripId,
          trip_leg_id AS TripLegId,
          event_type AS EventType,
          title AS Title,
          location AS Location,
          start_local AS StartLocal,
          start_timezone_id AS StartTimeZoneId,
          end_local AS EndLocal,
          end_timezone_id AS EndTimeZoneId,
          confirmation_code AS ConfirmationCode,
          notes AS Notes,
          confidence AS Confidence,
          review_status AS ReviewStatus,
          created_at_utc AS CreatedAtUtc;
