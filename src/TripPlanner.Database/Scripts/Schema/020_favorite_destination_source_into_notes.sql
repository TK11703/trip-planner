UPDATE favorite_destinations
SET notes = concat_ws(E'\n\n', NULLIF(btrim(notes), ''), 'Source: ' || btrim(source))
WHERE NULLIF(btrim(source), '') IS NOT NULL;

ALTER TABLE favorite_destinations DROP COLUMN source;
