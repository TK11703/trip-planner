using TripPlanner.Contracts.Theme;
using TripPlanner.Database.Sql;
using TripPlanner.Database.ThemePreferences;
using TripPlanner.Database.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Database.Tests.ThemePreferences;

/// <summary>
/// A theme preference belongs to exactly one traveler, and saving again replaces rather than
/// accumulates. Both claims are about what the table enforces, so both are checked against a
/// real PostgreSQL instance.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(SharedPostgres.Name)]
public class ThemePreferenceRepositoryTests
{
    private readonly ThemePreferenceRepository _repository;

    public ThemePreferenceRepositoryTests(PostgresFixture fixture)
        => _repository = new ThemePreferenceRepository(
            new StubConnectionFactory(fixture.ConnectionString), new SqlFileProvider());

    private static string NewTraveler() => $"traveler-{Guid.NewGuid():N}";

    [Fact]
    public async Task Upsert_PersistsOnePreferencePerTraveler()
    {
        var traveler = NewTraveler();
        var now = DateTimeOffset.UtcNow;

        var first = await _repository.UpsertAsync(traveler, ThemeMode.Dark, now);
        Assert.Equal(ThemeMode.Dark, first.ThemeMode);

        // Saving again is a correction, not a second opinion.
        var second = await _repository.UpsertAsync(traveler, ThemeMode.Light, now.AddMinutes(5));

        Assert.Equal(ThemeMode.Light, second.ThemeMode);
        Assert.Equal(traveler, second.TravelerId);

        var read = await _repository.GetAsync(traveler);
        Assert.NotNull(read);
        Assert.Equal(ThemeMode.Light, read!.ThemeMode);
    }

    [Fact]
    public async Task Get_ReturnsOnlyRequestedTravelerPreference()
    {
        var mine = NewTraveler();
        var theirs = NewTraveler();

        await _repository.UpsertAsync(mine, ThemeMode.Dark, DateTimeOffset.UtcNow);
        await _repository.UpsertAsync(theirs, ThemeMode.Light, DateTimeOffset.UtcNow);

        Assert.Equal(ThemeMode.Dark, (await _repository.GetAsync(mine))!.ThemeMode);
        Assert.Equal(ThemeMode.Light, (await _repository.GetAsync(theirs))!.ThemeMode);
    }

    [Fact]
    public async Task Get_ReturnsNullForATravelerWhoNeverChose()
    {
        Assert.Null(await _repository.GetAsync(NewTraveler()));
    }
}
