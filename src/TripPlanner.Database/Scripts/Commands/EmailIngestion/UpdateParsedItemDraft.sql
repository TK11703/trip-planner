UPDATE parsed_item_drafts
SET trip_id = @TripId,
    trip_leg_id = @TripLegId,
    item_type = @ItemType,
    title = @Title,
    location = @Location,
    start_local = @StartLocal,
    start_timezone_id = @StartTimeZoneId,
    end_local = @EndLocal,
    end_timezone_id = @EndTimeZoneId,
    confirmation_code = @ConfirmationCode,
    notes = @Notes
WHERE parsed_item_draft_id = @ParsedItemDraftId
  AND user_id = @UserId
  AND review_status = 'pending_review'
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
