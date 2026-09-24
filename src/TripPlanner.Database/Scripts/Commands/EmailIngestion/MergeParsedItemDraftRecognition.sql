-- Applies re-recognition to a draft that predates transport recognition (FR-045, FR-046).
--
-- The merge rule, and the reason it lives in SQL rather than C#: a field is written only when it
-- is BOTH currently null AND absent from traveler_edited_fields. The second half is what makes a
-- deliberately cleared value stay cleared — a plain "is null" test would treat the traveler's
-- decision to empty a field as an invitation to refill it.
--
-- end_local and end_timezone_id are never merged, even when recognition supplies them. A leg
-- demands both, and FR-034 reserves them for the traveler, who accepts the one sanctioned
-- suggestion in the review screen rather than having a value arrive behind their back.
--
-- The state column is always written, so a draft is re-examined at most once whatever the
-- outcome: 'current' when recognition answered, 'unavailable' when it could not (FR-047).

UPDATE parsed_item_drafts
SET proposed_outcome = CASE
        WHEN @ProposedOutcome IS NOT NULL
             AND proposed_outcome = 'item'
             AND NOT ('proposed_outcome' = ANY (traveler_edited_fields))
        THEN @ProposedOutcome
        ELSE proposed_outcome END,

    origin = CASE
        WHEN origin IS NULL AND NOT ('origin' = ANY (traveler_edited_fields))
        THEN @Origin ELSE origin END,

    destination = CASE
        WHEN destination IS NULL AND NOT ('destination' = ANY (traveler_edited_fields))
        THEN @Destination ELSE destination END,

    transportation_mode = CASE
        WHEN transportation_mode IS NULL AND NOT ('transportation_mode' = ANY (traveler_edited_fields))
        THEN @TransportationMode ELSE transportation_mode END,

    travel_cost = CASE
        WHEN travel_cost IS NULL AND NOT ('travel_cost' = ANY (traveler_edited_fields))
        THEN @TravelCost ELSE travel_cost END,

    travel_cost_currency = CASE
        WHEN travel_cost_currency IS NULL AND NOT ('travel_cost_currency' = ANY (traveler_edited_fields))
        THEN @TravelCostCurrency ELSE travel_cost_currency END,

    title = CASE
        WHEN title IS NULL AND NOT ('title' = ANY (traveler_edited_fields))
        THEN @Title ELSE title END,

    location = CASE
        WHEN location IS NULL AND NOT ('location' = ANY (traveler_edited_fields))
        THEN @Location ELSE location END,

    confirmation_code = CASE
        WHEN confirmation_code IS NULL AND NOT ('confirmation_code' = ANY (traveler_edited_fields))
        THEN @ConfirmationCode ELSE confirmation_code END,

    start_local = CASE
        WHEN start_local IS NULL AND NOT ('start_local' = ANY (traveler_edited_fields))
        THEN @StartLocal ELSE start_local END,

    start_timezone_id = CASE
        WHEN start_timezone_id IS NULL AND NOT ('start_timezone_id' = ANY (traveler_edited_fields))
        THEN @StartTimeZoneId ELSE start_timezone_id END,

    transport_recognition_state = @TransportRecognitionState
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
