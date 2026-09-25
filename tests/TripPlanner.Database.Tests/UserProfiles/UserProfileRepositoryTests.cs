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

    public UserProfileRepositoryTests(PostgresFixture fixture)
        => _profiles = new UserProfileRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());

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
            personalization ?? new PersonalizationPreferences(null, null, null, null));

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
    public async Task UpdateAsync_PersistsAndClearsPersonalizationPreferences()
    {
        var userId = NewUser();
        await _profiles.EnsureFromAuthenticatedUserAsync(userId, null, null, null, null, DateTimeOffset.UtcNow);

        await _profiles.UpdateAsync(
            userId,
            Update(personalization: new PersonalizationPreferences("Hiking", "SEA", "Slow", "Step-free routes")),
            DateTimeOffset.UtcNow);

        var saved = await _profiles.GetAsync(userId);
        Assert.Equal("Hiking", saved!.PersonalizationPreferences.TravelInterests);
        Assert.Equal("SEA", saved.PersonalizationPreferences.HomeAirport);
        Assert.Equal("Slow", saved.PersonalizationPreferences.PreferredTravelStyle);
        Assert.Equal("Step-free routes", saved.PersonalizationPreferences.AccessibilityNotes);

        // Clearing is a decision the traveler is entitled to make, and it has to stick.
        await _profiles.UpdateAsync(
            userId,
            Update(personalization: new PersonalizationPreferences(null, "   ", null, null)),
            DateTimeOffset.UtcNow.AddMinutes(1));

        var cleared = await _profiles.GetAsync(userId);
        Assert.Null(cleared!.PersonalizationPreferences.TravelInterests);
        Assert.Null(cleared.PersonalizationPreferences.HomeAirport);
        Assert.Null(cleared.PersonalizationPreferences.PreferredTravelStyle);
        Assert.Null(cleared.PersonalizationPreferences.AccessibilityNotes);
    }

    // ---- Notification preferences ----

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
