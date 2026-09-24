-- Recomputing traveler_edited_fields here is what protects a traveler's work from being
-- overwritten when a legacy draft is later re-recognized (FR-046). A field is recorded the
-- moment the submitted value differs from the stored one, including when it is cleared to null
-- — that is precisely the case a plain "is null" check in the merge would get wrong.
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
    notes = @Notes,
    proposed_outcome = @ProposedOutcome,
    origin = @Origin,
    destination = @Destination,
    transportation_mode = @TransportationMode,
    travel_cost = @TravelCost,
    traveler_edited_fields = ARRAY(
        SELECT DISTINCT unnest(
            traveler_edited_fields
            || CASE WHEN @ProposedOutcome IS DISTINCT FROM proposed_outcome
                    THEN ARRAY['proposed_outcome'] ELSE ARRAY[]::text[] END
            || CASE WHEN @Origin IS DISTINCT FROM origin
                    THEN ARRAY['origin'] ELSE ARRAY[]::text[] END
            || CASE WHEN @Destination IS DISTINCT FROM destination
                    THEN ARRAY['destination'] ELSE ARRAY[]::text[] END
            || CASE WHEN @TransportationMode IS DISTINCT FROM transportation_mode
                    THEN ARRAY['transportation_mode'] ELSE ARRAY[]::text[] END
            || CASE WHEN @TravelCost IS DISTINCT FROM travel_cost
                    THEN ARRAY['travel_cost'] ELSE ARRAY[]::text[] END))
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
          tracked_item_id AS TrackedItemId,
          proposed_outcome AS ProposedOutcome,
          origin AS Origin,
          destination AS Destination,
          transportation_mode AS TransportationMode,
          travel_cost AS TravelCost,
          travel_cost_currency AS TravelCostCurrency,
          created_trip_leg_id AS CreatedTripLegId,
          transport_recognition_state AS TransportRecognitionState,
          traveler_edited_fields AS TravelerEditedFields;
