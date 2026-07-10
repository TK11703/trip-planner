using TripPlanner.Contracts.Notifications;

namespace TripPlanner.Contracts.Profile;

public sealed record UserProfileResponse(
    string UserId,
    string? FirstName,
    string? LastName,
    string? DisplayName,
    string? Email,
    string TimeZoneId,
    string MapProvider,
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
    string MapProvider,
    NotificationPreferences NotificationPreferences,
    PersonalizationPreferences PersonalizationPreferences);

/// <summary>
/// The mapping tool used when opening a single event location. Persisted per user; defaults to Bing.
/// </summary>
public static class MapProviders
{
    public const string Bing = "Bing";
    public const string Google = "Google";
    public const string Default = Bing;

    /// <summary>Normalizes any input to a known provider, defaulting to Bing (never throws).</summary>
    public static string Normalize(string? value) =>
        string.Equals(value, Google, StringComparison.OrdinalIgnoreCase) ? Google : Bing;
}

/// <summary>
/// A person's consolidated notification preferences, one entry per user-controllable category.
/// This is the single, profile-owned surface for viewing and editing preferences.
/// </summary>
public sealed record NotificationPreferences(IReadOnlyList<NotificationCategoryPreference> Categories)
{
    /// <summary>All categories at their defaults, used when a person has no saved preferences.</summary>
    public static NotificationPreferences Default { get; } = new(
        NotificationCategories.All
            .Select(c => new NotificationCategoryPreference(
                c.Category,
                c.DisplayName,
                c.DefaultInAppEnabled,
                c.DefaultEmailEnabled,
                NotificationPreferenceSource.Default,
                UpdatedAtUtc: null))
            .ToArray());

    /// <summary>Finds the preference for a category, or null when not present.</summary>
    public NotificationCategoryPreference? Find(string category)
        => Categories.FirstOrDefault(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));
}

/// <summary>A person's delivery choice for a single notification category.</summary>
public sealed record NotificationCategoryPreference(
    string Category,
    string DisplayName,
    bool InAppEnabled,
    bool EmailEnabled,
    string Source = NotificationPreferenceSource.Saved,
    DateTimeOffset? UpdatedAtUtc = null);

/// <summary>Whether a shown preference came from a saved choice or a category default.</summary>
public static class NotificationPreferenceSource
{
    public const string Saved = "Saved";
    public const string Default = "Default";
}

public sealed record PersonalizationPreferences(
    string? TravelInterests,
    string? HomeAirport,
    string? PreferredTravelStyle,
    string? AccessibilityNotes);
