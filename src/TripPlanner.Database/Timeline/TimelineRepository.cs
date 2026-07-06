using Dapper;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Timeline;

public interface ITimelineRepository
{
    Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(string ownerUserId, Guid tripId, CancellationToken ct);
}

public sealed class TimelineRepository : ITimelineRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;
    public TimelineRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql) { _factory = factory; _sql = sql; }

    public async Task<IReadOnlyList<TimelineEvent>> GetTimelineAsync(string ownerUserId, Guid tripId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/Timeline/GetTripTimeline.sql");
        var rows = await conn.QueryAsync<TimelineRow>(new CommandDefinition(query, new { OwnerUserId = ownerUserId, TripId = tripId }, cancellationToken: ct));
        return rows.Select(r => new TimelineEvent(
            r.Id,
            r.SourceType,
            r.Title,
            r.Start,
            r.End,
            r.CalendarStart,
            r.CalendarEnd,
            r.StartTimeZoneId,
            r.StartTimeZoneLabel,
            r.EndTimeZoneId,
            r.EndTimeZoneLabel,
            r.AllDay,
            r.DisplayOrder,
            BuildMetadata(r))).ToArray();
    }

    private static Dictionary<string, string?>? BuildMetadata(TimelineRow row)
    {
        if (row.SourceType != "trip-leg")
        {
            return null;
        }

        return new Dictionary<string, string?>
        {
            ["startTimeZoneId"] = row.StartTimeZoneId,
            ["startTimeZoneLabel"] = row.StartTimeZoneLabel,
            ["endTimeZoneId"] = row.EndTimeZoneId,
            ["endTimeZoneLabel"] = row.EndTimeZoneLabel
        };
    }

    private sealed record TimelineRow(
        string Id,
        string SourceType,
        string Title,
        DateTimeOffset Start,
        DateTimeOffset? End,
        string CalendarStart,
        string? CalendarEnd,
        string? StartTimeZoneId,
        string? StartTimeZoneLabel,
        string? EndTimeZoneId,
        string? EndTimeZoneLabel,
        bool AllDay,
        int DisplayOrder);
}
