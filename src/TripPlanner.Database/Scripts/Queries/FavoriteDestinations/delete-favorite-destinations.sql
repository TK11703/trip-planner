DELETE FROM favorite_destinations
WHERE owner_user_id = @OwnerUserId
  AND favorite_destination_id = ANY(@FavoriteDestinationIds::uuid[]);
