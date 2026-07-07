-- Feature 010: resolve a caller's access to a trip. Returns the trip's owner id and the caller's
-- access level ('owner', 'collaborator', or 'viewer'). A share can be matched either by the caller's
-- stable id or by their email (for people invited by email before they first sign in). Returns no
-- rows when the caller has no access, so the API can deny without revealing the trip's existence.
SELECT
    t.owner_user_id AS "OwnerUserId",
    CASE WHEN t.owner_user_id = @OwnerUserId THEN 'owner' ELSE s.access_level END AS "AccessLevel"
FROM trips t
LEFT JOIN trip_shares s ON s.trip_id = t.trip_id
    AND (s.member_user_id = @OwnerUserId
         OR (@CallerEmail IS NOT NULL AND s.member_email IS NOT NULL AND lower(s.member_email) = lower(@CallerEmail)))
WHERE t.trip_id = @TripId
  AND (t.owner_user_id = @OwnerUserId
       OR s.member_user_id = @OwnerUserId
       OR (@CallerEmail IS NOT NULL AND s.member_email IS NOT NULL AND lower(s.member_email) = lower(@CallerEmail)))
LIMIT 1;
