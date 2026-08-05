INSERT INTO parsed_item_drafts (
    parsed_item_draft_id, inbox_email_id, user_id, trip_id, trip_leg_id,
    item_type, title, location,
    start_local, start_timezone_id, end_local, end_timezone_id,
    confirmation_code, notes, confidence, review_status)
VALUES (
    @ParsedItemDraftId, @InboxEmailId, @UserId, @TripId, @TripLegId,
    @ItemType, @Title, @Location,
    @StartLocal, @StartTimeZoneId, @EndLocal, @EndTimeZoneId,
    @ConfirmationCode, @Notes, @Confidence, 'pending_review')
RETURNING parsed_item_draft_id AS ParsedItemDraftId,
          inbox_email_id AS InboxEmailId,
          user_id AS UserId,
          trip_id AS TripId,
          trip_leg_id AS TripLegId,
          item_type AS ItemType,
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
          created_at_utc AS CreatedAtUtc,
          tracked_item_id AS TrackedItemId;
