using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Features.Timezones;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;
using Xunit;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// The matcher only suggests. It reads legs the caller can edit and reports whether exactly one
/// covers the draft, several do, or none does. Feature 024, FR-001 – FR-006.
/// </summary>
public class DraftPlacementMatcherTests
{
    private static DraftPlacementMatcher NewMatcher() => new(new TimezoneIdValidator());

    private static ParsedItemDraftRecord Draft(
        DateTime? startLocal,
        string? startTimeZoneId = "UTC",
        DateTime? endLocal = null,
        string? endTimeZoneId = null) => new(
            Guid.NewGuid(), Guid.NewGuid(), "user-1", null, null, "hotel", "Hotel Kabuki", "San Francisco, CA",
            startLocal, startTimeZoneId, endLocal, endTimeZoneId, null, null, 0.9, "pending_review", DateTimeOffset.UtcNow);

    // A candidate row is a trip paired with one of its legs. Unless a test says otherwise the
    // trip's own dates are taken from the leg, which keeps the leg-matching cases readable.
    private static PlacementCandidateLeg Leg(
        DateTimeOffset start,
        DateTimeOffset? end,
        string title = "San Francisco",
        Guid? tripId = null,
        DateOnly? tripStart = null,
        DateOnly? tripEnd = null)
        => new(tripId ?? Guid.NewGuid(), "West Coast, August",
            tripStart ?? DateOnly.FromDateTime(start.UtcDateTime),
            tripEnd ?? DateOnly.FromDateTime((end ?? start).UtcDateTime),
            Guid.NewGuid(), title, start, end);

    /// <summary>A trip the caller can edit that has no legs planned yet.</summary>
    private static PlacementCandidateLeg TripWithNoLegs(DateOnly tripStart, DateOnly tripEnd)
        => new(Guid.NewGuid(), "West Coast, August", tripStart, tripEnd, null, null, null, null);

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0)
        => new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExactlyOneContainingLeg_IsMatched_AndSuggestsBothIds()
    {
        var leg = Leg(Utc(2026, 8, 12), Utc(2026, 8, 16));
        var other = Leg(Utc(2026, 9, 1), Utc(2026, 9, 5), "Portland");

        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 15, 0, 0)),
            new[] { leg, other });

        Assert.Equal(DraftPlacementStatus.Matched, placement.Status);
        Assert.Equal(leg.TripId, placement.SuggestedTripId);
        Assert.Equal(leg.TripLegId, placement.SuggestedTripLegId);
        Assert.Single(placement.Candidates);
    }

    [Fact]
    public void TwoContainingLegs_AreAmbiguous_WithNoSuggestion()
    {
        var first = Leg(Utc(2026, 8, 12), Utc(2026, 8, 16));
        var second = Leg(Utc(2026, 8, 10), Utc(2026, 8, 20), "Bay Area");

        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 15, 0, 0)),
            new[] { first, second });

        Assert.Equal(DraftPlacementStatus.Ambiguous, placement.Status);
        Assert.Null(placement.SuggestedTripId);
        Assert.Null(placement.SuggestedTripLegId);
        Assert.Equal(2, placement.Candidates.Count);
    }

    [Fact]
    public void DraftWithNoStart_IsInsufficientData()
    {
        var placement = NewMatcher().Match(
            Draft(startLocal: null),
            new[] { Leg(Utc(2026, 8, 12), Utc(2026, 8, 16)) });

        Assert.Equal(DraftPlacementStatus.InsufficientData, placement.Status);
        Assert.Empty(placement.Candidates);
    }

    [Fact]
    public void DraftWithUnknownTimeZone_IsInsufficientData()
    {
        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 15, 0, 0), startTimeZoneId: "Not/AZone"),
            new[] { Leg(Utc(2026, 8, 12), Utc(2026, 8, 16)) });

        Assert.Equal(DraftPlacementStatus.InsufficientData, placement.Status);
    }

    [Fact]
    public void LegWithNoEnd_IsOpenEnded_AndCoversAnythingAfterItsStart()
    {
        var openEnded = Leg(Utc(2026, 8, 12), null);

        var placement = NewMatcher().Match(
            Draft(new DateTime(2027, 3, 1, 9, 0, 0)),
            new[] { openEnded });

        Assert.Equal(DraftPlacementStatus.Matched, placement.Status);
        Assert.Equal(openEnded.TripLegId, placement.SuggestedTripLegId);
    }

    // FR-002: containment is judged on instants, so a Tokyo wall clock lands correctly inside a
    // Pacific-zoned leg even though the two dates read differently.
    [Fact]
    public void ContainmentIsJudgedOnInstants_NotWallClock()
    {
        // 2026-08-13 07:00 in Tokyo is 2026-08-12 22:00 UTC.
        var leg = Leg(Utc(2026, 8, 12, 20), Utc(2026, 8, 12, 23));

        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 7, 0, 0), startTimeZoneId: "Asia/Tokyo"),
            new[] { leg });

        Assert.Equal(DraftPlacementStatus.Matched, placement.Status);
    }

    [Fact]
    public void DraftEndingAfterTheLeg_IsNotCovered()
    {
        var leg = Leg(Utc(2026, 8, 12), Utc(2026, 8, 16));

        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 15, 9, 0, 0), "UTC", new DateTime(2026, 8, 18, 9, 0, 0), "UTC"),
            new[] { leg });

        Assert.Equal(DraftPlacementStatus.NoLegCovers, placement.Status);
        Assert.Empty(placement.Candidates);
    }

    // FR-003: a viewer cannot add items, so the query never returns their legs and the matcher
    // therefore has nothing to offer — the draft belongs to no trip the caller can edit.
    [Fact]
    public void NoEditableLegs_YieldNoCandidates()
    {
        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 15, 0, 0)),
            Array.Empty<PlacementCandidateLeg>());

        Assert.Equal(DraftPlacementStatus.OutsideTripDates, placement.Status);
        Assert.Empty(placement.Candidates);
    }

    // FR-009: the dates fall inside a planned trip but land in a gap between its legs. That is a
    // different problem from an email that has nothing to do with any trip, and the traveler is
    // told so.
    [Fact]
    public void DraftInAGapBetweenLegs_IsNoLegCovers()
    {
        var tripId = Guid.NewGuid();
        var tripStart = new DateOnly(2026, 8, 10);
        var tripEnd = new DateOnly(2026, 8, 20);
        var first = Leg(Utc(2026, 8, 10), Utc(2026, 8, 14), "San Francisco", tripId, tripStart, tripEnd);
        var second = Leg(Utc(2026, 8, 18), Utc(2026, 8, 20), "Portland", tripId, tripStart, tripEnd);

        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 16, 15, 0, 0)),
            new[] { first, second });

        Assert.Equal(DraftPlacementStatus.NoLegCovers, placement.Status);
        Assert.Empty(placement.Candidates);
    }

    // FR-010: nothing planned covers these dates at all.
    [Fact]
    public void DraftOutsideEveryTripsDates_IsOutsideTripDates()
    {
        var placement = NewMatcher().Match(
            Draft(new DateTime(2027, 4, 2, 15, 0, 0)),
            new[] { Leg(Utc(2026, 8, 12), Utc(2026, 8, 16)) });

        Assert.Equal(DraftPlacementStatus.OutsideTripDates, placement.Status);
        Assert.Empty(placement.Candidates);
    }

    // FR-014: a trip that exists but has no legs yet is a gap, not an error.
    [Fact]
    public void TripWithNoLegsPlanned_IsNoLegCovers()
    {
        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 15, 0, 0)),
            new[] { TripWithNoLegs(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20)) });

        Assert.Equal(DraftPlacementStatus.NoLegCovers, placement.Status);
        Assert.Empty(placement.Candidates);
    }

    /// <summary>
    /// Feature 025: a flight, train, bus, or boat leg can never hold an item, so the candidate
    /// query never returns one. A trip whose only legs are restricted therefore arrives here as a
    /// trip row with no leg — and the draft lands in the same "no leg covers it" gap as a trip with
    /// nothing planned, which the traveler can still confirm as unassigned.
    /// </summary>
    [Fact]
    public void TripWhoseOnlyLegsAreRestricted_IsNoLegCovers()
    {
        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 15, 0, 0)),
            new[] { TripWithNoLegs(new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20)) });

        Assert.Equal(DraftPlacementStatus.NoLegCovers, placement.Status);
        Assert.Empty(placement.Candidates);
        Assert.Null(placement.SuggestedTripLegId);
    }

    /// <summary>Only the eligible legs reach the matcher, so a covering Car leg still matches even
    /// when the trip also contains restricted legs over the same dates.</summary>
    [Fact]
    public void EligibleLegStillMatches_WhenOnlyEligibleLegsAreSupplied()
    {
        var tripId = Guid.NewGuid();
        var carLeg = Leg(Utc(2026, 8, 12), Utc(2026, 8, 16), "Road trip", tripId,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20));

        var placement = NewMatcher().Match(
            Draft(new DateTime(2026, 8, 13, 15, 0, 0)),
            new[] { carLeg });

        Assert.Equal(DraftPlacementStatus.Matched, placement.Status);
        Assert.Equal(carLeg.TripLegId, placement.SuggestedTripLegId);
    }
}
