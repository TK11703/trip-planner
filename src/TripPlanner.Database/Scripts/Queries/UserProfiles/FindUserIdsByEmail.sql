-- Resolves the traveler that owns a relayed message from the normalized sender address.
-- Returns every match so the caller can reject ambiguous (multiple) results.
SELECT user_id
FROM users
WHERE email IS NOT NULL
  AND lower(btrim(email)) = @Email
ORDER BY user_id;
