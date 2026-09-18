using System.Globalization;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;

namespace TripPlanner.Web.Features.Trips;

/// <summary>
/// Pure, side-effect-free helpers that project a <see cref="TripDetail"/> into the
/// view models rendered by the printable trip page. All datetime values are the
/// traveler's local wall-clock times, combined with their timezone into a single
/// column, formatted <c>MM/dd/yyyy HH:mm TZ</c> where TZ is the short zone name
/// (for example <c>EDT</c> or <c>JST</c>).
/// </summary>
public static class TripPrintFormatting
{
    /// <summary>
    /// Combines a local wall-clock datetime with its timezone into a single, self-contained
    /// value: <c>MM/dd/yyyy HH:mm {shortZone}</c> (24-hour, invariant culture). Never throws;
    /// a null/blank zone simply appends nothing after the time.
    /// </summary>
    public static string FormatDateTimeWithZone(DateTime local, string? timeZoneId)
    {
        var stamp = local.ToString("MM/dd/yyyy HH:mm", CultureInfo.InvariantCulture);
        var zone = GetShortZoneName(timeZoneId, local);
        return string.IsNullOrEmpty(zone) ? stamp : $"{stamp} {zone}";
    }

    /// <summary>
    /// Resolves a short timezone abbreviation (e.g. <c>EDT</c>, <c>PST</c>, <c>JST</c>) for the given
    /// local time, honoring daylight saving for that specific date. Falls back to the raw id when the
    /// zone is unknown or an abbreviation cannot be derived, and returns an empty string for a blank id.
    /// </summary>
    public static string GetShortZoneName(string? timeZoneId, DateTime local)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return string.Empty;
        }

        var id = timeZoneId.Trim();
        if (string.Equals(id, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            return "UTC";
        }

        var tz = TimezoneOptions.FindTimeZone(id);
        if (tz is null)
        {
            return id;
        }

        var inDst = tz.IsDaylightSavingTime(local);
        var name = inDst ? tz.DaylightName : tz.StandardName;
        var abbreviation = Abbreviate(name);
        return string.IsNullOrEmpty(abbreviation) ? id : abbreviation;
    }

    private static readonly char[] ZoneNameSeparators = [' ', '-'];

    private static string Abbreviate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var initials = name
            .Split(ZoneNameSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => char.IsLetter(word[0]))
            .Select(word => char.ToUpperInvariant(word[0]))
            .ToArray();

        return initials.Length >= 2 ? new string(initials) : string.Empty;
    }

    /// <summary>Legs in chronological order by start, with a stable tie-break on sort order then title.</summary>
    public static IReadOnlyList<TripLegDto> OrderLegsChronologically(IEnumerable<TripLegDto> legs) =>
        legs
            .OrderBy(l => l.StartLocal)
            .ThenBy(l => l.SortOrder)
            .ThenBy(l => l.Title, StringComparer.Ordinal)
            .ToList();

    /// <summary>Items in chronological order by start, tie-broken by sort order.</summary>
    public static IReadOnlyList<TrackedItemDto> OrderItemsWithinLeg(IEnumerable<TrackedItemDto> items) =>
        items
            .OrderBy(i => i.StartLocal)
            .ThenBy(i => i.SortOrder)
            .ToList();

    /// <summary>
    /// Partitions the trip's tracked items by owning leg. Items whose <c>TripLegId</c> is null or
    /// does not match any leg are returned separately as "unassigned".
    /// </summary>
    public static (IReadOnlyDictionary<Guid, IReadOnlyList<TrackedItemDto>> ByLeg, IReadOnlyList<TrackedItemDto> Unassigned) GroupItemsByLeg(
        IEnumerable<TripLegDto> legs, IEnumerable<TrackedItemDto> items)
    {
        var legIds = legs.Select(l => l.TripLegId).ToHashSet();
        var byLeg = new Dictionary<Guid, List<TrackedItemDto>>();
        var unassigned = new List<TrackedItemDto>();

        foreach (var item in items)
        {
            if (item.TripLegId is { } legId && legIds.Contains(legId))
            {
                if (!byLeg.TryGetValue(legId, out var list))
                {
                    list = new List<TrackedItemDto>();
                    byLeg[legId] = list;
                }

                list.Add(item);
            }
            else
            {
                unassigned.Add(item);
            }
        }

        var projected = byLeg.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<TrackedItemDto>)OrderItemsWithinLeg(kvp.Value));

        return (projected, OrderItemsWithinLeg(unassigned));
    }

    /// <summary>Builds the full printable view model for a trip.</summary>
    public static PrintableTrip BuildPrintableTrip(TripDetail trip)
    {
        var (byLeg, unassigned) = GroupItemsByLeg(trip.Legs, trip.TrackedItems);

        var legs = OrderLegsChronologically(trip.Legs)
            .Select(leg => new PrintableLeg(
                leg.TripLegId,
                leg.Title,
                BuildRouteText(leg.Origin, leg.Destination),
                FormatDateTimeWithZone(leg.StartLocal, leg.StartTimeZoneId),
                FormatDateTimeWithZone(leg.EndLocal, leg.EndTimeZoneId),
                byLeg.TryGetValue(leg.TripLegId, out var legItems)
                    ? legItems.Select(ToPrintableItem).ToList()
                    : Array.Empty<PrintableItem>(),
                // Only a travel leg has a mode or booking details; a stay prints without them.
                TransportationModes.Label(leg.TransportationMode) is { Length: > 0 } mode ? mode : null,
                string.IsNullOrWhiteSpace(leg.ConfirmationCode) ? null : leg.ConfirmationCode,
                leg.TravelCost is { } travelCost ? travelCost.ToString("C", CultureInfo.CurrentCulture) : null))
            .ToList();

        var description = string.IsNullOrWhiteSpace(trip.Description) ? null : trip.Description;

        return new PrintableTrip(
            trip.Name,
            BuildDateRangeText(trip.StartDate, trip.EndDate),
            description,
            trip.EstimatedCostTotal.ToString("C", CultureInfo.CurrentCulture),
            legs,
            unassigned.Select(ToPrintableItem).ToList());
    }

    private static PrintableItem ToPrintableItem(TrackedItemDto item) => new(
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(item.ItemType),
        item.Title,
        string.IsNullOrWhiteSpace(item.Location) ? null : item.Location,
        FormatDateTimeWithZone(item.StartLocal, item.StartTimeZoneId),
        item.EndLocal is { } end ? FormatDateTimeWithZone(end, item.EndTimeZoneId ?? item.StartTimeZoneId) : null,
        string.IsNullOrWhiteSpace(item.ConfirmationCode) ? null : item.ConfirmationCode,
        item.EstimatedCost is { } cost ? cost.ToString("C", CultureInfo.CurrentCulture) : null);

    private static string? BuildRouteText(string? origin, string? destination)
    {
        var parts = new[] { origin, destination }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToArray();
        return parts.Length == 0 ? null : string.Join(" \u2192 ", parts);
    }

    private static string BuildDateRangeText(DateOnly start, DateOnly end) =>
        $"{start.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)} \u2013 {end.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture)}";
}

/// <summary>Top-level printable projection of a single trip.</summary>
public sealed record PrintableTrip(
    string Name,
    string DateRangeText,
    string? Description,
    string EstimatedCostText,
    IReadOnlyList<PrintableLeg> Legs,
    IReadOnlyList<PrintableItem> UnassignedItems)
{
    /// <summary>True when the trip has at least one leg or item to print.</summary>
    public bool HasContent => Legs.Count > 0 || UnassignedItems.Count > 0;
}

/// <summary>A leg rendered as a chronological row-divider grouping its items.</summary>
public sealed record PrintableLeg(
    Guid TripLegId,
    string Title,
    string? RouteText,
    string StartText,
    string EndText,
    IReadOnlyList<PrintableItem> Items,
    string? ModeText = null,
    string? ConfirmationCode = null,
    string? TravelCostText = null);

/// <summary>An item row; one property per printed column.</summary>
public sealed record PrintableItem(
    string TypeText,
    string Title,
    string? Location,
    string StartText,
    string? EndText,
    string? ConfirmationCode,
    string? EstimatedCostText);
