-- Feature 010: change an existing member's access level. Only the trip owner may update.
-- Returns the updated share row, or no rows when the caller is not the owner or no share exists.
UPDATE trip_shares s
SET access_level = @AccessLevel,
    updated_at_utc = @NowUtc
FROM trips t
WHERE s.trip_id = @TripId
  AND s.member_user_id = @MemberUserId
  AND t.trip_id = s.trip_id
  AND t.owner_user_id = @OwnerUserId
RETURNING
    s.member_user_id      AS "UserId",
    s.member_display_name AS "DisplayName",
    s.member_email        AS "Email",
    s.access_level        AS "AccessLevel",
    s.updated_at_utc      AS "UpdatedAtUtc";
