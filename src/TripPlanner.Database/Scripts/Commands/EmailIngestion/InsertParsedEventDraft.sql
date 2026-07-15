INSERT INTO parsed_event_drafts (
    parsed_event_draft_id, inbox_email_id, user_id, trip_id, trip_leg_id,
    event_type, title, location,
    start_local, start_timezone_id, end_local, end_timezone_id,
    confirmation_code, notes, confidence, review_status)
VALUES (
    @ParsedEventDraftId, @InboxEmailId, @UserId, @TripId, @TripLegId,
    @EventType, @Title, @Location,
    @StartLocal, @StartTimeZoneId, @EndLocal, @EndTimeZoneId,
    @ConfirmationCode, @Notes, @Confidence, 'pending_review')
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
