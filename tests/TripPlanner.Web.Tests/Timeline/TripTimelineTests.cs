using Bunit;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Timeline;

public class TripTimelineTests : BunitContext
{
    public TripTimelineTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static TimelineLeg Leg(string title, DateTime start, DateTime end, params TimelineItem[] items)
        => new(
            Guid.NewGuid(),
            title,
            null,
            null,
            start,
            "UTC",
            "UTC",
            end,
            "UTC",
            "UTC",
            0,
            items);

    private static TimelineItem Item(Guid? legId, string title, DateTime start)
        => new(
            Guid.NewGuid(),
            legId,
            "activity",
            title,
            null,
            start,
            "UTC",
            new DateTimeOffset(start, TimeSpan.Zero),
            null,
            null,
            null,
            "blue",
            false,
            false,
            0);

    private static TimelineItem ItemWithEnd(Guid legId, string title, DateTime start, DateTime end)
        => new(
            Guid.NewGuid(),
            legId,
            "activity",
            title,
            null,
            start,
            "UTC",
            new DateTimeOffset(start, TimeSpan.Zero),
            end,
            "UTC",
            new DateTimeOffset(end, TimeSpan.Zero),
            "blue",
            false,
            false,
            0);

    private static TripTimelineResponse Response(params TimelineLeg[] legs)
        => new(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            30,
            legs,
            Array.Empty<TimelineItem>());

    // Feature 024, FR-012: an item confirmed without a leg still shows up on the timeline, with
    // its dates, in a lane that reads as waiting to be related to a leg.
    [Fact]
    public void AnItemWithNoLeg_AppearsInTheUnassignedLane_WithItsDates()
    {
        var unassigned = Item(null, "Hotel Kabuki", new DateTime(2026, 9, 2, 15, 0, 0));
        var response = new TripTimelineResponse(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 3),
            30,
            Array.Empty<TimelineLeg>(),
            new[] { unassigned });
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unassigned", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Hotel Kabuki", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("assign it to a leg", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void LegRow_WithNoItems_ShowsZeroCount()
    {
        var response = Response(Leg("Paris", new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 2, 8, 0, 0)));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("0 items", cut.Markup));
    }

    [Fact]
    public void LegRow_WithOneItem_ShowsSingularCount()
    {
        var legId = Guid.NewGuid();
        var leg = new TimelineLeg(legId, "Paris", null, null,
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            new[] { Item(legId, "Dinner", new DateTime(2026, 9, 1, 19, 0, 0)) });
        var response = Response(leg);
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("1 item", cut.Markup));
        Assert.DoesNotContain("1 items", cut.Markup);
    }

    [Fact]
    public void LegRow_WithMultipleItems_ShowsPluralCount()
    {
        var legId = Guid.NewGuid();
        var leg = new TimelineLeg(legId, "Paris", null, null,
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            new[]
            {
                Item(legId, "Dinner", new DateTime(2026, 9, 1, 19, 0, 0)),
                Item(legId, "Museum", new DateTime(2026, 9, 1, 10, 0, 0)),
                Item(legId, "Walk", new DateTime(2026, 9, 1, 14, 0, 0))
            });
        var response = Response(leg);
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("3 items", cut.Markup));
    }

    [Fact]
    public void EveryLeg_ShowsItsOwnIndependentCount()
    {
        var legAId = Guid.NewGuid();
        var legA = new TimelineLeg(legAId, "Paris", null, null,
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            new[] { Item(legAId, "Dinner", new DateTime(2026, 9, 1, 19, 0, 0)) });
        var legB = Leg("Rome", new DateTime(2026, 9, 2, 8, 0, 0), new DateTime(2026, 9, 3, 8, 0, 0));
        var response = Response(legA, legB);
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() =>
        {
            var counts = cut.FindAll(".ttl-leg-count");
            Assert.Equal(2, counts.Count);
            Assert.Equal("1 item", counts[0].TextContent.Trim());
            Assert.Equal("0 items", counts[1].TextContent.Trim());
        });
    }

    [Fact]
    public void AddItemButton_EmitsSlotSelectionForThatLegWithLegStart()
    {
        var legStart = new DateTime(2026, 9, 1, 8, 0, 0);
        var leg = Leg("Paris", legStart, new DateTime(2026, 9, 2, 8, 0, 0));
        var response = Response(leg);

        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));
        TripTimeline.TimelineSlotSelection? captured = null;
        var cut = Render<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.OnLegSlotSelected, s => captured = s));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ttl-leg-add")));
        cut.Find(".ttl-leg-add").Click();

        Assert.NotNull(captured);
        Assert.Equal(leg.TripLegId, captured!.TripLegId);
        Assert.Equal(legStart, captured.StartLocal);
    }

    [Fact]
    public void OverlappingItems_StackOnSeparateLanes()
    {
        var legId = Guid.NewGuid();
        // Timeshare Stay spans the afternoon; Dinner starts within it -> they overlap.
        var stay = ItemWithEnd(legId, "Timeshare Stay", new DateTime(2026, 9, 1, 12, 0, 0), new DateTime(2026, 9, 1, 20, 0, 0));
        var dinner = ItemWithEnd(legId, "Dinner", new DateTime(2026, 9, 1, 18, 0, 0), new DateTime(2026, 9, 1, 19, 0, 0));
        var leg = new TimelineLeg(legId, "Oahu - Ko Olina", null, null,
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            new[] { stay, dinner });
        var response = Response(leg);
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll(".ttl-item");
            Assert.Equal(2, items.Count);
            var tops = items
                .Select(i => i.GetAttribute("style"))
                .Select(ExtractTopRem)
                .ToList();
            // The two overlapping items must sit at different vertical offsets.
            Assert.NotEqual(tops[0], tops[1]);
        });
    }

    [Fact]
    public void OverlappingItems_GrowLegRowHeight()
    {
        var legId = Guid.NewGuid();
        var stay = ItemWithEnd(legId, "Timeshare Stay", new DateTime(2026, 9, 1, 12, 0, 0), new DateTime(2026, 9, 1, 20, 0, 0));
        var dinner = ItemWithEnd(legId, "Dinner", new DateTime(2026, 9, 1, 18, 0, 0), new DateTime(2026, 9, 1, 19, 0, 0));
        var leg = new TimelineLeg(legId, "Oahu - Ko Olina", null, null,
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            new[] { stay, dinner });
        var response = Response(leg);
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() =>
        {
            var lane = cut.FindAll(".ttl-lane")[0];
            var minHeight = ExtractMinHeightRem(lane.GetAttribute("style"));
            // Two stacked lanes must reserve more than the default single-row height (3.75rem).
            Assert.True(minHeight > 3.75, $"Expected grown row, got {minHeight}rem.");
        });
    }

    // --- Feature 025: what the leg is, and where items may go ---

    private static TimelineLeg ModeLeg(string? legKind, string? mode, decimal? travelCost = null, string? confirmationCode = null)
        => new(
            Guid.NewGuid(), "Getting there", "Paris", "Chicago",
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            Array.Empty<TimelineItem>(), 0m, legKind, mode, travelCost, confirmationCode);

    [Theory]
    [InlineData(TransportationModes.Flight, "Flight")]
    [InlineData(TransportationModes.Train, "Train")]
    [InlineData(TransportationModes.Bus, "Bus")]
    [InlineData(TransportationModes.Boat, "Boat")]
    [InlineData(TransportationModes.Car, "Car")]
    public void TravelLeg_ShowsHowTheTravelerIsGettingThere(string mode, string label)
    {
        var response = Response(ModeLeg(TripLegKinds.Travel, mode));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Equal(label, cut.Find("[data-testid=leg-mode]").TextContent.Trim()));
        Assert.Contains("Paris", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Chicago", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void StayLeg_ShowsNoTransportationMode()
    {
        var response = Response(ModeLeg(TripLegKinds.Stay, null));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ttl-leg-label")));
        Assert.Empty(cut.FindAll("[data-testid=leg-mode]"));
    }

    [Fact]
    public void TravelLeg_ShowsConfirmationCodeWhenPresent()
    {
        var response = Response(ModeLeg(TripLegKinds.Travel, TransportationModes.Flight, confirmationCode: "ABC123"));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("ABC123", cut.Find("[data-testid=leg-confirmation]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void TravelLeg_WithoutBookingDetails_ShowsNeither()
    {
        var response = Response(ModeLeg(TripLegKinds.Travel, TransportationModes.Flight));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ttl-leg-label")));
        Assert.Empty(cut.FindAll("[data-testid=leg-confirmation]"));
        Assert.Empty(cut.FindAll("[data-testid=leg-travel-cost]"));
    }

    /// <summary>Travel cost is the leg's own price, kept apart from the total its items add up to.</summary>
    [Fact]
    public void TravelCost_IsShownSeparatelyFromTheItemTotal()
    {
        var response = Response(ModeLeg(TripLegKinds.Travel, TransportationModes.Car, travelCost: 412.50m));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("Travel cost", cut.Find("[data-testid=leg-travel-cost]").TextContent, StringComparison.Ordinal));
        Assert.Contains("Estimated total", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    public void RestrictedLeg_OffersNoWayToAddAnItem(string mode)
    {
        var response = Response(ModeLeg(TripLegKinds.Travel, mode));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ttl-leg-label")));
        Assert.Empty(cut.FindAll(".ttl-lane.ttl-lane-clickable"));
        Assert.Empty(cut.FindAll(".ttl-leg-label .ttl-leg-add"));
    }

    [Theory]
    [InlineData(TripLegKinds.Travel, TransportationModes.Car)]
    [InlineData(TripLegKinds.Stay, null)]
    public void EligibleLeg_KeepsItsAddItemAction(string legKind, string? mode)
    {
        var response = Response(ModeLeg(legKind, mode));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = Render<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ttl-leg-label .ttl-leg-add")));
        Assert.NotEmpty(cut.FindAll(".ttl-lane.ttl-lane-clickable"));
    }

    /// <summary>Clicking a restricted leg's lane cannot start an item the API would refuse.</summary>
    [Fact]
    public void ClickingARestrictedLegsLane_SelectsNothing()
    {
        var response = Response(ModeLeg(TripLegKinds.Travel, TransportationModes.Flight));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        TripTimeline.TimelineSlotSelection? captured = null;
        var cut = Render<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.OnLegSlotSelected, s => captured = s));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ttl-lane")));
        cut.FindAll(".ttl-lane")[0].Click();

        Assert.Null(captured);
    }

    private static double ExtractTopRem(string? style)
    {
        var match = Regex.Match(style ?? string.Empty, @"top:\s*([0-9.]+)rem");
        return match.Success ? double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
    }

    private static double ExtractMinHeightRem(string? style)
    {
        var match = Regex.Match(style ?? string.Empty, @"min-height:\s*([0-9.]+)rem");
        return match.Success ? double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
    }

    private sealed class StubTripApiClient : ITripApiClient
    {
        private readonly TripTimelineResponse _timeline;
        public StubTripApiClient(TripTimelineResponse timeline) => _timeline = timeline;

        public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default)
            => Task.FromResult<TripTimelineResponse?>(_timeline);

        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>>(Array.Empty<TripPlanner.Contracts.Places.PlaceSuggestion>());
        public Task<TripPlanner.Contracts.Trips.TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(new TripPlanner.Contracts.Trips.TripMapResponse(Array.Empty<TripPlanner.Contracts.Trips.TripMapLocation>()));
    }
}
