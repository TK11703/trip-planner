using TripPlanner.Contracts.Profile;
using TripPlanner.Web.Features.Profile;

namespace TripPlanner.Web.Tests.Profile;

internal sealed class RecordingProfileApiClient : IProfileApiClient
{
    private UserProfileResponse _profile;
    private readonly Func<UpdateUserProfileRequest, UserProfileResponse>? _save;

    public RecordingProfileApiClient(UserProfileResponse profile, Func<UpdateUserProfileRequest, UserProfileResponse>? save = null)
    {
        _profile = profile;
        _save = save;
    }

    public int SaveCallCount { get; private set; }
    public UpdateUserProfileRequest? LastRequest { get; private set; }

    public Task<UserProfileResponse?> GetAsync(CancellationToken ct = default)
        => Task.FromResult<UserProfileResponse?>(_profile);

    public Task<UserProfileResponse> SaveAsync(UpdateUserProfileRequest request, CancellationToken ct = default)
    {
        SaveCallCount++;
        LastRequest = request;
        _profile = _save?.Invoke(request) ?? _profile with
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DisplayName = request.DisplayName,
            Email = request.Email,
            TimeZoneId = request.TimeZoneId,
            NotificationPreferences = request.NotificationPreferences,
            PersonalizationPreferences = request.PersonalizationPreferences,
            IsComplete = !string.IsNullOrWhiteSpace(request.DisplayName) && !string.IsNullOrWhiteSpace(request.Email)
        };
        return Task.FromResult(_profile);
    }
}

internal sealed class ThrowingProfileApiClient : IProfileApiClient
{
    private readonly UserProfileResponse _profile;

    public ThrowingProfileApiClient(UserProfileResponse profile) => _profile = profile;

    public Task<UserProfileResponse?> GetAsync(CancellationToken ct = default)
        => Task.FromResult<UserProfileResponse?>(_profile);

    public Task<UserProfileResponse> SaveAsync(UpdateUserProfileRequest request, CancellationToken ct = default)
        => throw new InvalidOperationException("Enter a valid email address.");
}

internal static class ProfileTestData
{
    public static UserProfileResponse CompleteProfile(
        NotificationPreferences? notifications = null,
        PersonalizationPreferences? personalization = null)
        => new(
            "user-a-immutable-id",
            "Avery",
            "Traveler",
            "Avery Traveler",
            "avery@example.test",
            "UTC",
            true,
            notifications ?? new NotificationPreferences(false, false, false),
            personalization ?? new PersonalizationPreferences(null, null, null, null),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
}
