using TripPlanner.Contracts.Profile;
using TripPlanner.Database.UserProfiles;

namespace TripPlanner.Api.Tests.UserProfiles;

internal sealed class InMemoryUserProfileRepository : IUserProfileRepository
{
    private readonly Dictionary<string, UserProfileResponse> _profiles = new(StringComparer.Ordinal);

    public Task<UserProfileResponse?> GetAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_profiles.TryGetValue(userId, out var profile) ? profile : null);

    public Task<UserProfileResponse> EnsureFromAuthenticatedUserAsync(string userId, string? firstName, string? lastName, string? displayName, string? email, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        if (_profiles.TryGetValue(userId, out var existing))
        {
            var seen = existing with { LastSeenAtUtc = nowUtc };
            _profiles[userId] = seen;
            return Task.FromResult(seen);
        }

        var profile = new UserProfileResponse(
            userId,
            Normalize(firstName),
            Normalize(lastName),
            Normalize(displayName) ?? BuildDisplayName(firstName, lastName),
            Normalize(email),
            IsComplete: !string.IsNullOrWhiteSpace(displayName ?? BuildDisplayName(firstName, lastName)) && !string.IsNullOrWhiteSpace(email),
            new NotificationPreferences(false, false, false),
            new PersonalizationPreferences(null, null, null, null),
            nowUtc,
            nowUtc,
            nowUtc);
        _profiles[userId] = profile;
        return Task.FromResult(profile);
    }

    public Task<UserProfileResponse?> UpdateAsync(string userId, UpdateUserProfileRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        if (!_profiles.TryGetValue(userId, out var existing))
        {
            return Task.FromResult<UserProfileResponse?>(null);
        }

        var displayName = Normalize(request.DisplayName) ?? BuildDisplayName(request.FirstName, request.LastName);
        var updated = existing with
        {
            FirstName = Normalize(request.FirstName),
            LastName = Normalize(request.LastName),
            DisplayName = displayName,
            Email = Normalize(request.Email),
            IsComplete = !string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(request.Email),
            NotificationPreferences = request.NotificationPreferences,
            PersonalizationPreferences = new PersonalizationPreferences(
                Normalize(request.PersonalizationPreferences.TravelInterests),
                Normalize(request.PersonalizationPreferences.HomeAirport),
                Normalize(request.PersonalizationPreferences.PreferredTravelStyle),
                Normalize(request.PersonalizationPreferences.AccessibilityNotes)),
            UpdatedAtUtc = nowUtc,
            LastSeenAtUtc = nowUtc
        };
        _profiles[userId] = updated;
        return Task.FromResult<UserProfileResponse?>(updated);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var parts = new[] { Normalize(firstName), Normalize(lastName) }.Where(part => part is not null);
        var displayName = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }
}
