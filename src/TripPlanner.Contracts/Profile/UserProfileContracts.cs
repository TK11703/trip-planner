namespace TripPlanner.Contracts.Profile;

public sealed record UserProfileResponse(
    string UserId,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Email,
    string TimeZoneId,
    bool IsComplete,
    NotificationPreferences NotificationPreferences,
    PersonalizationPreferences PersonalizationPreferences,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset LastSeenAtUtc);

public sealed record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Email,
    string TimeZoneId,
    NotificationPreferences NotificationPreferences,
    PersonalizationPreferences PersonalizationPreferences);

public sealed record NotificationPreferences(
    bool EmailNotificationsEnabled,
    bool TripReminderNotificationsEnabled,
    bool ItineraryChangeNotificationsEnabled);

public sealed record PersonalizationPreferences(
    string? TravelInterests,
    string? HomeAirport,
    string? PreferredTravelStyle,
    string? AccessibilityNotes);
