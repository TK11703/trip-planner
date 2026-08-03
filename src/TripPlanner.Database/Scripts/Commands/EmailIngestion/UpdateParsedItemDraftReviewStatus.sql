UPDATE parsed_item_drafts
SET review_status = @ReviewStatus
WHERE parsed_item_draft_id = @ParsedItemDraftId
  AND user_id = @UserId
  AND review_status = 'pending_review'
RETURNING parsed_item_draft_id AS ParsedItemDraftId,
          review_status AS ReviewStatus;
