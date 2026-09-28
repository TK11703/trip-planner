SELECT EXISTS (
   SELECT 1
   FROM trips t
   WHERE t.owner_user_id = @CallerUserId
     OR EXISTS (
        SELECT 1
        FROM trip_shares s
        WHERE s.trip_id = t.trip_id
          AND (
                s.member_user_id = @CallerUserId
                OR (
                    @CallerEmail IS NOT NULL
                    AND s.member_email IS NOT NULL
                    AND lower(s.member_email) = lower(@CallerEmail)
                )
          )
    )
  );