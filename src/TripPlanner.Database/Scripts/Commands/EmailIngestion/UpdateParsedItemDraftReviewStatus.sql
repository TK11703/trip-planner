UPDATE parsed_item_drafts
-- COALESCE keeps a previously recorded id when a status change carries none, so a discard
-- cannot erase the trace of what this draft already produced (FR-025, FR-038).
SET review_status = @ReviewStatus,
    tracked_item_id = COALESCE(@TrackedItemId, tracked_item_id),
    created_trip_leg_id = COALESCE(@CreatedTripLegId, created_trip_leg_id),
    proposed_outcome = COALESCE(@ProposedOutcome, proposed_outcome)
WHERE parsed_item_draft_id = @ParsedItemDraftId
  AND user_id = @UserId
  AND review_status = 'pending_review'
RETURNING parsed_item_draft_id AS ParsedItemDraftId,
          review_status AS ReviewStatus,
          tracked_item_id AS TrackedItemId,
          created_trip_leg_id AS CreatedTripLegId,
          proposed_outcome AS ProposedOutcome;
