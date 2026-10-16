using Dapper;
using Npgsql;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Contracts.Profile;
using TripPlanner.Database.Sql;
using TripPlanner.Database.Tests.Infrastructure;
using TripPlanner.Database.UserProfiles;
using Xunit;

namespace TripPlanner.Database.Tests.UserProfiles;

/// <summary>
/// The profile is the one record a traveler edits about themselves, and it is written from two
/// directions: the sign-in claims that seed it, and the form that corrects it. The rule that
/// matters is that sign-in must never overwrite what the traveler saved — a rule expressed
/// entirely in the <c>ON CONFLICT</c> clause, so it can only be verified against a real database.
///
/// The five classes these replace were empty skipped placeholders; they are one class here
/// because they share a fixture and a seeding helper, and splitting them again would only spread
/// the same setup across five files.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class UserProfileRepositoryTests
{
    private readonly UserProfileRepository _profiles;
    private readonly string _connectionString;

    public UserProfileRepositoryTests(PostgresFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _profiles = new UserProfileRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());
    }

    private static string NewUser() => $"user-{Guid.NewGuid():N}";

    private static UpdateUserProfileRequest Update(
        string? firstName = "Ada",
        string? lastName = "Lovelace",
        string? displayName = "Ada Lovelace",
        string? email = "ada@contoso.com",
        string timeZoneId = "America/New_York",
        string mapProvider = MapProviders.Google,
        PersonalizationPreferences? personalization = null,
        NotificationPreferences? notifications = null) =>
        new(firstName, lastName, displayName, email, timeZoneId, mapProvider,
            notifications ?? NotificationPreferences.Default,
            personalization ?? new PersonalizationPreferences());

    // ---- Seeding from sign-in claims ----

    [Fact]
    public async Task EnsureFromAuthenticatedUser_CreatesOnceAndDoesNotOverwriteSavedValues()
    {
        var userId = NewUser();
        var firstSeen = DateTimeOffset.UtcNow;

        var created = await _profiles.EnsureFromAuthenticatedUserAsync(
            userId, "Ada", "Lovelace", null, "ada@contoso.com", firstSeen);

        Assert.Equal(userId, created.UserId);
        Assert.Equal("Ada Lovelace", created.DisplayName);

        // The traveler then corrects their own record.
        await _profiles.UpdateAsync(userId, Update(displayName: "Ada L.", email: "ada.l@contoso.com"), firstSeen.AddMinutes(1));

        // Signing in again must not undo that, however stale the claims are.
        var secondSignIn = await _profiles.EnsureFromAuthenticatedUserAsync(
            userId, "Ada", "Lovelace", "Ada Lovelace", "ada@contoso.com", firstSeen.AddHours(1));

        Assert.Equal("Ada L.", secondSignIn.DisplayName);
        Assert.Equal("ada.l@contoso.com", secondSignIn.Email);
        Assert.Equal("America/New_York", secondSignIn.TimeZoneId);

        // Only the last-seen stamp moves forward.
        Assert.True(secondSignIn.LastSeenAtUtc > created.LastSeenAtUtc);
        Assert.Equal(created.CreatedAtUtc, secondSignIn.CreatedAtUtc);
    }

    // ---- Identity and contact fields ----

    [Fact]
    public async Task UpdateAsync_PersistsIdentityAndContactFieldsWithoutChangingUserId()
    {
        var userId = NewUser();
        await _profiles.EnsureFromAuthenticatedUserAsync(userId, null, null, null, null, DateTimeOffset.UtcNow);

        var updated = await _profiles.UpdateAsync(
            userId,
            Update(firstName: "Grace", lastName: "Hopper", displayName: "Grace Hopper", email: "grace@contoso.com"),
            DateTimeOffset.UtcNow);

        Assert.NotNull(updated);
        Assert.Equal(userId, updated!.UserId);
        Assert.Equal("Grace", updated.FirstName);
        Assert.Equal("Hopper", updated.LastName);
        Assert.Equal("Grace Hopper", updated.DisplayName);
        Assert.Equal("grace@contoso.com", updated.Email);

        var read = await _profiles.GetAsync(userId);
        Assert.Equal("Grace Hopper", read!.DisplayName);
        Assert.Equal(userId, read.UserId);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNullForAProfileThatDoesNotExist()
    {
        Assert.Null(await _profiles.UpdateAsync(NewUser(), Update(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task UpdateAsync_TouchesOnlyTheRequestedProfile()
    {
        var mine = NewUser();
        var theirs = NewUser();
        await _profiles.EnsureFromAuthenticatedUserAsync(mine, "Mine", null, "Mine", "mine@contoso.com", DateTimeOffset.UtcNow);
        await _profiles.EnsureFromAuthenticatedUserAsync(theirs, "Theirs", null, "Theirs", "theirs@contoso.com", DateTimeOffset.UtcNow);

        await _profiles.UpdateAsync(mine, Update(displayName: "Changed"), DateTimeOffset.UtcNow);

        Assert.Equal("Theirs", (await _profiles.GetAsync(theirs))!.DisplayName);
    }

    // ---- Map provider ----

    [Theory]
    [InlineData("Google", MapProviders.Google)]
    [InlineData("google", MapProviders.Google)]
    [InlineData("Bing", MapProviders.Bing)]
    [InlineData("nonsense", MapProviders.Bing)] // Anything unrecognized falls back to the default.
    public async Task UpdateAsync_PersistsMapProvider_AndReadsBackCanonically(string supplied, string expected)
    {
        var userId = NewUser();
        await _profiles.EnsureFromAuthenticatedUserAsync(userId, null, null, null, null, DateTimeOffset.UtcNow);

        var updated = await _profiles.UpdateAsync(userId, Update(mapProvider: supplied), DateTimeOffset.UtcNow);

        Assert.Equal(expected, updated!.MapProvider);
        Assert.Equal(expected, (await _profiles.GetAsync(userId))!.MapProvider);
    }

    // ---- Personalization ----

    [Fact]
    public async Task RemoveUnusedProfileFields_DropsRetiredColumnsAndPreservesUserAndColors()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("""
            CREATE TEMP TABLE users (
                user_id text PRIMARY KEY,
                display_name text,
                item_type_colors jsonb,
                travel_interests text,
                home_airport text,
                preferred_travel_style text,
                accessibility_notes text,
                personalization_updated_at_utc timestamptz
            ) ON COMMIT DROP;
            INSERT INTO users (
                user_id, display_name, item_type_colors, travel_interests, home_airport,
                preferred_travel_style, accessibility_notes, personalization_updated_at_utc)
            VALUES (
                @UserId, @DisplayName, CAST(@Colors AS jsonb), @Interests, @Airport,
                @Style, @Notes, @Now);
            """, new
        {
            UserId = "migration-user",
            DisplayName = "Traveler",
            Colors = """{"activity":"purple"}""",
            Interests = "Museums",
            Airport = "SEA",
            Style = "Slow",
            Notes = "Quiet rooms",
            Now = DateTimeOffset.UtcNow
        }, transaction);

        await connection.ExecuteAsync(
            new SqlFileProvider().Get("Schema/025_remove_unused_profile_fields.sql"), transaction: transaction);

        var columns = await connection.QueryAsync<string>("""
            SELECT attname
            FROM pg_attribute
            WHERE attrelid = 'pg_temp.users'::regclass AND attnum > 0 AND NOT attisdropped
            ORDER BY attname;
            """, transaction: transaction);
        Assert.Equal(["display_name", "item_type_colors", "user_id"], columns);
        Assert.Equal("Traveler", await connection.QuerySingleAsync<string>(
            "SELECT display_name FROM users WHERE user_id = @UserId;",
            new { UserId = "migration-user" }, transaction));
        Assert.Equal("purple", await connection.QuerySingleAsync<string>(
            "SELECT item_type_colors ->> 'activity' FROM users WHERE user_id = @UserId;",
            new { UserId = "migration-user" }, transaction));
        await transaction.CommitAsync();
    }

    // ---- Notification preferences ----

    [Fact]
    public async Task ItemTypeColors_PersistPerUser_SurviveSignInAndOlderClients_AndClear()
    {
        var mine = NewUser();
        var theirs = NewUser();
        var now = DateTimeOffset.UtcNow;
        await _profiles.EnsureFromAuthenticatedUserAsync(mine, null, null, null, null, now);
        await _profiles.EnsureFromAuthenticatedUserAsync(theirs, null, null, null, null, now);
        Assert.Empty((await _profiles.GetAsync(mine))!.PersonalizationPreferences.ItemTypeColors!);

        await _profiles.UpdateAsync(mine, Update(personalization: new(
            new Dictionary<string, string> { ["Activity"] = "PURPLE", ["event"] = "teal" })), now);
        var saved = await _profiles.GetAsync(mine);
        Assert.Equal("purple", saved!.PersonalizationPreferences.ItemTypeColors!["activity"]);
        Assert.Equal("teal", saved.PersonalizationPreferences.GetItemColor("event"));
        Assert.Equal("slate", (await _profiles.GetAsync(theirs))!.PersonalizationPreferences.GetItemColor("activity"));

        var signedIn = await _profiles.EnsureFromAuthenticatedUserAsync(mine, null, null, null, null, now.AddMinutes(1));
        Assert.Equal("purple", signedIn.PersonalizationPreferences.GetItemColor("activity"));
        await _profiles.UpdateAsync(mine, Update(), now.AddMinutes(2));
        Assert.Equal("purple", (await _profiles.GetAsync(mine))!.PersonalizationPreferences.GetItemColor("activity"));

        await _profiles.UpdateAsync(mine, Update(personalization: new(
            new Dictionary<string, string>())), now.AddMinutes(3));
        var cleared = await _profiles.GetAsync(mine);
        Assert.Empty(cleared!.PersonalizationPreferences.ItemTypeColors!);
        Assert.Equal("slate", cleared.PersonalizationPreferences.GetItemColor("activity"));
    }

    [Fact]
    public async Task UpdateAsync_PersistsNotificationPreferences()
    {
        var userId = NewUser();
        await _profiles.EnsureFromAuthenticatedUserAsync(userId, null, null, null, null, DateTimeOffset.UtcNow);

        // Turn everything off, which is the opposite of every category default.
        var allOff = new NotificationPreferences(
            NotificationCategories.All
                .Select(c => new NotificationCategoryPreference(c.Category, c.DisplayName, false, false))
                .ToArray());

        var updated = await _profiles.UpdateAsync(userId, Update(notifications: allOff), DateTimeOffset.UtcNow);

        Assert.NotNull(updated);
        Assert.All(updated!.NotificationPreferences.Categories, c =>
        {
            Assert.False(c.InAppEnabled);
            Assert.False(c.EmailEnabled);
            Assert.Equal(NotificationPreferenceSource.Saved, c.Source);
        });

        // And they survive a fresh read rather than only being echoed back.
        var read = await _profiles.GetAsync(userId);
        Assert.All(read!.NotificationPreferences.Categories, c => Assert.False(c.InAppEnabled));
    }

    /// <summary>
    /// A traveler who has never touched preferences sees the category defaults, labelled as
    /// defaults rather than as choices they made.
    /// </summary>
    [Fact]
    public async Task AProfileWithNoSavedPreferencesReportsTheDefaults()
    {
        var userId = NewUser();
        var created = await _profiles.EnsureFromAuthenticatedUserAsync(userId, null, null, null, null, DateTimeOffset.UtcNow);

        Assert.All(created.NotificationPreferences.Categories,
            c => Assert.Equal(NotificationPreferenceSource.Default, c.Source));
    }

    /// <summary>An unknown category is ignored rather than stored, so the table cannot drift.</summary>
    [Fact]
    public async Task UpdateAsync_IgnoresAnUnknownNotificationCategory()
    {
        var userId = NewUser();
        await _profiles.EnsureFromAuthenticatedUserAsync(userId, null, null, null, null, DateTimeOffset.UtcNow);

        var withJunk = new NotificationPreferences(
        [
            new NotificationCategoryPreference("NotACategory", "Junk", false, false)
        ]);

        var updated = await _profiles.UpdateAsync(userId, Update(notifications: withJunk), DateTimeOffset.UtcNow);

        Assert.NotNull(updated);
        Assert.DoesNotContain(updated!.NotificationPreferences.Categories, c => c.Category == "NotACategory");
    }
}
