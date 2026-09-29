ALTER TABLE favorite_destinations
    ADD COLUMN latitude double precision,
    ADD COLUMN longitude double precision,
    ADD CONSTRAINT favorite_destinations_coordinates_pair_chk
        CHECK ((latitude IS NULL) = (longitude IS NULL)),
    ADD CONSTRAINT favorite_destinations_latitude_range_chk
        CHECK (latitude IS NULL OR latitude BETWEEN -90 AND 90),
    ADD CONSTRAINT favorite_destinations_longitude_range_chk
        CHECK (longitude IS NULL OR longitude BETWEEN -180 AND 180);
