using Dapper;
using TripPlanner.Contracts.Theme;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.ThemePreferences;

public sealed class ThemePreferenceRepository : IThemePreferenceRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;

    public ThemePreferenceRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql)
    {
        _factory = factory;
        _sql = sql;
    }

    public async Task<ThemePreferenceRecord?> GetAsync(string travelerId, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var query = _sql.Get("Queries/ThemePreferences/GetThemePreference.sql");
        var row = await conn.QuerySingleOrDefaultAsync<ThemePreferenceRow>(new CommandDefinition(query, new { TravelerId = travelerId }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task<ThemePreferenceRecord> UpsertAsync(string travelerId, ThemeMode themeMode, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(cancellationToken);
        var command = _sql.Get("Commands/ThemePreferences/UpsertThemePreference.sql");
        var row = await conn.QuerySingleAsync<ThemePreferenceRow>(new CommandDefinition(command, new { TravelerId = travelerId, ThemeMode = ToDatabaseValue(themeMode), NowUtc = nowUtc }, cancellationToken: cancellationToken));
        return row.ToRecord();
    }

    private static string ToDatabaseValue(ThemeMode mode) => mode == ThemeMode.Dark ? "dark" : "light";

    private sealed record ThemePreferenceRow(string TravelerId, string ThemeMode, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc)
    {
        public ThemePreferenceRecord ToRecord() => new(TravelerId, string.Equals(ThemeMode, "dark", StringComparison.OrdinalIgnoreCase) ? global::TripPlanner.Contracts.Theme.ThemeMode.Dark : global::TripPlanner.Contracts.Theme.ThemeMode.Light, CreatedAtUtc, UpdatedAtUtc);
    }
}
