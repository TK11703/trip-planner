UPDATE parsed_item_drafts
-- COALESCE keeps a previously recorded item id when a status change carries none, so a
-- discard cannot erase the trace of an item this draft already produced (FR-025).
SET review_status = @ReviewStatus,
    tracked_item_id = COALESCE(@TrackedItemId, tracked_item_id)
WHERE parsed_item_draft_id = @ParsedItemDraftId
  AND user_id = @UserId
  AND review_status = 'pending_review'
RETURNING parsed_item_draft_id AS ParsedItemDraftId,
          review_status AS ReviewStatus,
          tracked_item_id AS TrackedItemId;
