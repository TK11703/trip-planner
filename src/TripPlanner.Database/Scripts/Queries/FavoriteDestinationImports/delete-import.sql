DELETE FROM favorite_destination_imports
WHERE import_id = @ImportId
  AND owner_user_id = @OwnerUserId;
