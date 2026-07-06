using Dapper;
using TripPlanner.Contracts.Profile;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.UserProfiles;

public interface IUserProfileRepository
{
    Task<UserProfileResponse?> GetAsync(string userId, CancellationToken cancellationToken = default);
    Task<UserProfileResponse> EnsureFromAuthenticatedUserAsync(string userId, string? firstName, string? lastName, string? displayName, string? email, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
    Task<UserProfileResponse?> UpdateAsync(string userId, UpdateUserProfileRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public UserProfileRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<UserProfileResponse?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var query = _sql.Get("Queries/UserProfiles/GetUserProfile.sql");
        var row = await conn.QuerySingleOrDefaultAsync<UserProfileRow>(new CommandDefinition(query, new { UserId = userId }, cancellationToken: cancellationToken));
        return row?.ToResponse();
    }

    public async Task<UserProfileResponse> EnsureFromAuthenticatedUserAsync(string userId, string? firstName, string? lastName, string? displayName, string? email, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var command = _sql.Get("Commands/UserProfiles/EnsureUserProfileFromClaims.sql");
        var row = await conn.QuerySingleAsync<UserProfileRow>(new CommandDefinition(command, new
        {
            UserId = userId,
            FirstName = Normalize(firstName),
            LastName = Normalize(lastName),
            DisplayName = Normalize(displayName) ?? BuildDisplayName(firstName, lastName),
            Email = Normalize(email),
            NowUtc = nowUtc
        }, cancellationToken: cancellationToken));

        return row.ToResponse();
    }

    public async Task<UserProfileResponse?> UpdateAsync(string userId, UpdateUserProfileRequest request, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var command = _sql.Get("Commands/UserProfiles/UpdateUserProfile.sql");
        var row = await conn.QuerySingleOrDefaultAsync<UserProfileRow>(new CommandDefinition(command, new
        {
            UserId = userId,
            FirstName = Normalize(request.FirstName),
            LastName = Normalize(request.LastName),
            DisplayName = Normalize(request.DisplayName) ?? BuildDisplayName(request.FirstName, request.LastName),
            Email = Normalize(request.Email),
            request.NotificationPreferences.EmailNotificationsEnabled,
            request.NotificationPreferences.TripReminderNotificationsEnabled,
            request.NotificationPreferences.ItineraryChangeNotificationsEnabled,
            TravelInterests = Normalize(request.PersonalizationPreferences.TravelInterests),
            HomeAirport = Normalize(request.PersonalizationPreferences.HomeAirport),
            PreferredTravelStyle = Normalize(request.PersonalizationPreferences.PreferredTravelStyle),
            AccessibilityNotes = Normalize(request.PersonalizationPreferences.AccessibilityNotes),
            NowUtc = nowUtc
        }, cancellationToken: cancellationToken));

        return row?.ToResponse();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? BuildDisplayName(string? firstName, string? lastName)
    {
        var parts = new[] { Normalize(firstName), Normalize(lastName) }.Where(part => part is not null);
        var displayName = string.Join(' ', parts);
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }

    private sealed record UserProfileRow(
        string UserId,
        string? FirstName,
        string? LastName,
        string? DisplayName,
        string? Email,
        bool EmailNotificationsEnabled,
        bool TripReminderNotificationsEnabled,
        bool ItineraryChangeNotificationsEnabled,
        string? TravelInterests,
        string? HomeAirport,
        string? PreferredTravelStyle,
        string? AccessibilityNotes,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DateTimeOffset LastSeenAtUtc)
    {
        public UserProfileResponse ToResponse() => new(
            UserId,
            FirstName,
            LastName,
            DisplayName,
            Email,
            IsComplete: !string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(Email),
            new NotificationPreferences(EmailNotificationsEnabled, TripReminderNotificationsEnabled, ItineraryChangeNotificationsEnabled),
            new PersonalizationPreferences(TravelInterests, HomeAirport, PreferredTravelStyle, AccessibilityNotes),
            CreatedAtUtc,
            UpdatedAtUtc,
            LastSeenAtUtc);
    }
}
