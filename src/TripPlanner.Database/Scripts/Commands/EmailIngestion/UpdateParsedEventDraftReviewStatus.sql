UPDATE parsed_event_drafts
SET review_status = @ReviewStatus
WHERE parsed_event_draft_id = @ParsedEventDraftId
  AND user_id = @UserId
  AND review_status = 'pending_review'
RETURNING parsed_event_draft_id AS ParsedEventDraftId,
          review_status AS ReviewStatus;
