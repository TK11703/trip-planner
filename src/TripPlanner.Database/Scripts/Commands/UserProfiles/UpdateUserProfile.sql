UPDATE users
SET
    first_name = @FirstName,
    last_name = @LastName,
    display_name = @DisplayName,
    email = @Email,
    email_notifications_enabled = @EmailNotificationsEnabled,
    trip_reminder_notifications_enabled = @TripReminderNotificationsEnabled,
    itinerary_change_notifications_enabled = @ItineraryChangeNotificationsEnabled,
    notification_updated_at_utc = @NowUtc,
    travel_interests = @TravelInterests,
    home_airport = @HomeAirport,
    preferred_travel_style = @PreferredTravelStyle,
    accessibility_notes = @AccessibilityNotes,
    personalization_updated_at_utc = @NowUtc,
    last_seen_at_utc = @NowUtc
WHERE user_id = @UserId
RETURNING
    user_id AS UserId,
    first_name AS FirstName,
    last_name AS LastName,
    display_name AS DisplayName,
    email AS Email,
    email_notifications_enabled AS EmailNotificationsEnabled,
    trip_reminder_notifications_enabled AS TripReminderNotificationsEnabled,
    itinerary_change_notifications_enabled AS ItineraryChangeNotificationsEnabled,
    travel_interests AS TravelInterests,
    home_airport AS HomeAirport,
    preferred_travel_style AS PreferredTravelStyle,
    accessibility_notes AS AccessibilityNotes,
    created_at_utc AS CreatedAtUtc,
    updated_at_utc AS UpdatedAtUtc,
    last_seen_at_utc AS LastSeenAtUtc;
