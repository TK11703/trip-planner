-- Feature 010: everyone with access to a trip, ordered with the owner first, then members by name.
-- The owner is not stored as a share row, so it is unioned in from the trip and users table.
SELECT
    combined.user_id        AS "UserId",
    combined.display_name   AS "DisplayName",
    combined.email          AS "Email",
    combined.access_level   AS "AccessLevel",
    combined.updated_at_utc AS "UpdatedAtUtc"
FROM (
    SELECT
        t.owner_user_id AS user_id,
        u.display_name  AS display_name,
        u.email         AS email,
        'owner'         AS access_level,
        t.updated_at_utc AS updated_at_utc,
        0               AS sort_rank
    FROM trips t
    LEFT JOIN users u ON u.user_id = t.owner_user_id
    WHERE t.trip_id = @TripId
    UNION ALL
    SELECT
        s.member_user_id,
        s.member_display_name,
        s.member_email,
        s.access_level,
        s.updated_at_utc,
        1
    FROM trip_shares s
    WHERE s.trip_id = @TripId
) combined
ORDER BY combined.sort_rank, combined.display_name NULLS LAST, combined.user_id;
