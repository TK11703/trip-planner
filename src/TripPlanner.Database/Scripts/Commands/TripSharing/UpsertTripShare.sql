-- Feature 010: create or update a share. Only the trip owner may write, and the owner cannot be
-- added as a member. Returns the resulting share row, or no rows when the caller is not the owner.
INSERT INTO trip_shares (trip_share_id, trip_id, member_user_id, member_display_name, member_email, access_level, created_by_user_id, created_at_utc, updated_at_utc)
SELECT @TripShareId, @TripId, @MemberUserId, @MemberDisplayName, @MemberEmail, @AccessLevel, @OwnerUserId, @NowUtc, @NowUtc
FROM trips t
WHERE t.trip_id = @TripId
  AND t.owner_user_id = @OwnerUserId
  AND t.owner_user_id <> @MemberUserId
ON CONFLICT (trip_id, member_user_id)
DO UPDATE SET
    access_level = EXCLUDED.access_level,
    member_display_name = EXCLUDED.member_display_name,
    member_email = EXCLUDED.member_email,
    updated_at_utc = @NowUtc
RETURNING
    member_user_id      AS "UserId",
    member_display_name AS "DisplayName",
    member_email        AS "Email",
    access_level        AS "AccessLevel",
    updated_at_utc      AS "UpdatedAtUtc";
