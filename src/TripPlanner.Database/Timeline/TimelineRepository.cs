using Dapper;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.Connections;
using TripPlanner.Database.Sql;

namespace TripPlanner.Database.Timeline;

public sealed record TimelineProjection(
    IReadOnlyList<TimelineLeg> Legs,
    IReadOnlyList<TimelineItem> UnassignedItems);

public interface ITimelineRepository
{
    Task<TimelineProjection> GetTimelineAsync(string ownerUserId, Guid tripId, CancellationToken ct);
}

public sealed class TimelineRepository : ITimelineRepository
{
    private readonly IPostgresConnectionFactory _factory;
    private readonly ISqlFileProvider _sql;
    public TimelineRepository(IPostgresConnectionFactory factory, ISqlFileProvider sql) { _factory = factory; _sql = sql; }

    public async Task<TimelineProjection> GetTimelineAsync(string ownerUserId, Guid tripId, CancellationToken ct)
    {
        await using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var query = _sql.Get("Queries/Timeline/GetTripTimeline.sql");
        await using var grid = await conn.QueryMultipleAsync(new CommandDefinition(query, new { OwnerUserId = ownerUserId, TripId = tripId }, cancellationToken: ct));

        var legRows = (await grid.ReadAsync<LegRow>()).ToList();
        var itemRows = (await grid.ReadAsync<ItemRow>()).ToList();

        var itemsByLeg = itemRows
            .Where(i => i.TripLegId is not null)
            .GroupBy(i => i.TripLegId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var legs = new List<TimelineLeg>(legRows.Count);
        foreach (var leg in legRows)
        {
            var legItems = itemsByLeg.TryGetValue(leg.TripLegId, out var found)
                ? found
                    .OrderBy(i => i.StartsAt)
                    .ThenBy(i => i.SortOrder)
                    .ThenBy(i => i.Title, StringComparer.Ordinal)
                    .ThenBy(i => i.TrackedItemId)
                    .Select(i => MapItem(i, leg))
                    .ToArray()
                : Array.Empty<TimelineItem>();

            var legEstimatedTotal = legItems
                .Where(i => i.EstimatedCost is not null)
                .Sum(i => i.EstimatedCost!.Value);

            legs.Add(new TimelineLeg(
                leg.TripLegId,
                leg.Title,
                leg.Origin,
                leg.Destination,
                leg.StartLocal,
                leg.StartTimeZoneId,
                leg.StartTimeZoneId,
                leg.EndLocal,
                leg.EndTimeZoneId,
                leg.EndTimeZoneId,
                leg.SortOrder,
                legItems,
                legEstimatedTotal));
        }

        var unassigned = itemRows
            .Where(i => i.TripLegId is null)
            .OrderBy(i => i.StartsAt)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Title, StringComparer.Ordinal)
            .ThenBy(i => i.TrackedItemId)
            .Select(i => MapItem(i, null))
            .ToArray();

        return new TimelineProjection(legs, unassigned);
    }

    private static TimelineItem MapItem(ItemRow item, LegRow? leg)
    {
        var startsOutside = false;
        var endsOutside = false;
        if (leg is not null)
        {
            startsOutside = item.StartsAt < leg.StartAt || item.StartsAt > leg.EndAt;
            if (item.EndsAt is { } end)
            {
                endsOutside = end < leg.StartAt || end > leg.EndAt;
            }
        }

        return new TimelineItem(
            item.TrackedItemId,
            item.TripLegId,
            item.ItemType,
            item.Title,
            item.Location,
            item.StartLocal,
            item.StartTimeZoneId,
            item.StartsAt,
            item.EndLocal,
            item.EndTimeZoneId,
            item.EndsAt,
            TrackedItemColors.Normalize(item.DisplayColor),
            startsOutside,
            endsOutside,
            item.SortOrder,
            item.EstimatedCost);
    }

    private sealed record LegRow(
        Guid TripLegId,
        string Title,
        string? Origin,
        string? Destination,
        DateTime StartLocal,
        string StartTimeZoneId,
        DateTime EndLocal,
        string EndTimeZoneId,
        DateTimeOffset StartAt,
        DateTimeOffset EndAt,
        int SortOrder);

    private sealed record ItemRow(
        Guid TrackedItemId,
        Guid? TripLegId,
        string ItemType,
        string Title,
        string? Location,
        DateTime StartLocal,
        string StartTimeZoneId,
        DateTimeOffset StartsAt,
        DateTime? EndLocal,
        string? EndTimeZoneId,
        DateTimeOffset? EndsAt,
        string? DisplayColor,
        int SortOrder,
        decimal? EstimatedCost);
}
