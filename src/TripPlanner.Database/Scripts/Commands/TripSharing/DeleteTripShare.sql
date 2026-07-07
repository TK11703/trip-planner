-- Feature 010: remove a member's access. Only the trip owner may delete.
-- Affected row count is zero when the caller is not the owner or no share exists.
DELETE FROM trip_shares s
USING trips t
WHERE s.trip_id = @TripId
  AND s.member_user_id = @MemberUserId
  AND t.trip_id = s.trip_id
  AND t.owner_user_id = @OwnerUserId;
