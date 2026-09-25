using TripPlanner.Api.Features.Timezones;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Decides where an ingested email draft belongs by comparing its timeframe against the travel
/// windows of the legs the traveler can edit. Purely a suggestion: nothing here writes, and the
/// answer reflects only the legs that exist at the moment of the call (FR-001, FR-002, FR-006).
/// </summary>
public sealed class DraftPlacementMatcher
{
    private static readonly IReadOnlyList<PlacementCandidate> None = Array.Empty<PlacementCandidate>();

    private readonly ITimezoneIdValidator _timezones;

    public DraftPlacementMatcher(ITimezoneIdValidator timezones)
    {
        _timezones = timezones;
    }

    public DraftPlacement Match(ParsedItemDraftRecord draft, IReadOnlyList<PlacementCandidateLeg> candidateLegs)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(candidateLegs);

        // Containment is judged on instants, so a draft with no start — or one whose zone we
        // cannot resolve — cannot be placed at all.
        if (draft.StartLocal is not { } startLocal)
            return new DraftPlacement(DraftPlacementStatus.InsufficientData, null, null, None);

        var startZone = _timezones.FindTimeZone(draft.StartTimeZoneId ?? string.Empty);
        if (startZone is null)
            return new DraftPlacement(DraftPlacementStatus.InsufficientData, null, null, None);

        var start = TripInstant.ToInstant(startLocal, startZone);

        // Trip dates are calendar dates, so they are matched against the parsed local date rather
        // than an instant. A unique trip can be suggested even when it has no existing legs yet.
        var localDate = DateOnly.FromDateTime(startLocal);
        var localEndDate = DateOnly.FromDateTime(draft.EndLocal ?? startLocal);
        var isLegProposal = string.Equals(draft.ProposedOutcome, DraftOutcomes.Leg, StringComparison.Ordinal);
        var matchingTrips = candidateLegs
            .Where(candidate => localDate >= candidate.TripStart
                && localDate <= candidate.TripEnd
                && (!isLegProposal || localEndDate <= candidate.TripEnd))
            .DistinctBy(candidate => candidate.TripId)
            .ToArray();
        var suggestedTrip = isLegProposal
            ? matchingTrips
                .OrderBy(candidate => candidate.TripEnd.DayNumber - candidate.TripStart.DayNumber)
                .ThenByDescending(candidate => candidate.TripStart)
                .ThenBy(candidate => candidate.TripId)
                .FirstOrDefault()
            : matchingTrips.Length == 1 ? matchingTrips[0] : null;

        DateTimeOffset? end = null;
        if (draft.EndLocal is { } endLocal)
        {
            var endZone = _timezones.FindTimeZone(draft.EndTimeZoneId ?? draft.StartTimeZoneId ?? string.Empty);
            if (endZone is not null)
                end = TripInstant.ToInstant(endLocal, endZone);
        }

        var matches = candidateLegs
            .Where(leg => Covers(leg, start, end))
            .Select(leg => new PlacementCandidate(leg.TripId, leg.TripName, leg.TripLegId!.Value, leg.LegTitle!, leg.LegStart!.Value, leg.LegEnd))
            .ToArray();

        if (matches.Length == 1)
            return new DraftPlacement(DraftPlacementStatus.Matched, matches[0].TripId, matches[0].TripLegId, matches, matches[0].TripName);
        if (matches.Length > 1)
            return new DraftPlacement(
                DraftPlacementStatus.Ambiguous,
                suggestedTrip?.TripId,
                null,
                matches,
                suggestedTrip?.TripName);

        // No leg fits. Whether that is a gap the traveler can live with or a sign the email has
        // nothing to do with any planned trip depends on the trips' own dates (FR-009, FR-010).
        // Trips are planned in calendar dates, so the draft is judged by its local date here.
        var insideATrip = matchingTrips.Length > 0;

        return new DraftPlacement(
            insideATrip ? DraftPlacementStatus.NoLegCovers : DraftPlacementStatus.OutsideTripDates,
            suggestedTrip?.TripId, null, None, suggestedTrip?.TripName);
    }

    /// <summary>A leg covers a draft when the whole draft falls inside the leg's travel window.
    /// A leg with no end is open-ended, so anything at or after its start is inside it. A trip
    /// with no legs contributes no coverage at all.</summary>
    private static bool Covers(PlacementCandidateLeg leg, DateTimeOffset start, DateTimeOffset? end)
    {
        if (leg.TripLegId is null || leg.LegStart is not { } legStart)
            return false;
        if (start < legStart)
            return false;
        if (leg.LegEnd is not { } legEnd)
            return true;
        if (start > legEnd)
            return false;
        return end is not { } itemEnd || itemEnd <= legEnd;
    }
}
