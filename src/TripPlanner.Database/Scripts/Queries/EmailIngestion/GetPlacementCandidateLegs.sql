-- Feature 024: every trip leg on every trip the caller can edit, used to suggest where an
-- ingested email draft belongs. Mirrors the accessible-trips CTE in Queries/Trips/GetTripsPage.sql
-- but keeps only Owner and Collaborator access — a viewer cannot add items, so a viewer's trip
-- must never be offered as a placement (FR-003).
WITH editable AS (
    SELECT
        t.trip_id,
        t.name,
        t.start_date,
        t.end_date,
        t.owner_user_id
    FROM trips t
    WHERE t.owner_user_id = @OwnerUserId
    UNION
    SELECT
        t.trip_id,
        t.name,
        t.start_date,
        t.end_date,
        t.owner_user_id
    FROM trips t
    JOIN trip_shares s ON s.trip_id = t.trip_id
        AND (s.member_user_id = @OwnerUserId
             OR (@CallerEmail IS NOT NULL AND s.member_email IS NOT NULL AND lower(s.member_email) = lower(@CallerEmail)))
    WHERE s.access_level IN ('owner', 'collaborator')
)
-- LEFT JOIN so a trip with no legs still comes back: the traveler needs to be told the dates
-- fall inside that trip but no leg covers them, which is different from no trip at all (FR-014).
SELECT
    e.trip_id     AS "TripId",
    e.name        AS "TripName",
    e.start_date  AS "TripStart",
    e.end_date    AS "TripEnd",
    l.trip_leg_id AS "TripLegId",
    l.title       AS "LegTitle",
    l.start_at    AS "LegStart",
    l.end_at      AS "LegEnd"
FROM editable e
LEFT JOIN trip_legs l ON l.trip_id = e.trip_id AND l.owner_user_id = e.owner_user_id
ORDER BY l.start_at, l.sort_order, l.trip_leg_id;
