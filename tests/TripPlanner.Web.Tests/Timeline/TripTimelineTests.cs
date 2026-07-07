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

public class TripTimelineTests : TestContext
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

    private static TimelineItem Item(Guid legId, string title, DateTime start)
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

    [Fact]
    public void LegRow_WithNoEvents_ShowsZeroCount()
    {
        var response = Response(Leg("Paris", new DateTime(2026, 9, 1, 8, 0, 0), new DateTime(2026, 9, 2, 8, 0, 0)));
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("0 events", cut.Markup));
    }

    [Fact]
    public void LegRow_WithOneEvent_ShowsSingularCount()
    {
        var legId = Guid.NewGuid();
        var leg = new TimelineLeg(legId, "Paris", null, null,
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            new[] { Item(legId, "Dinner", new DateTime(2026, 9, 1, 19, 0, 0)) });
        var response = Response(leg);
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("1 event", cut.Markup));
        Assert.DoesNotContain("1 events", cut.Markup);
    }

    [Fact]
    public void LegRow_WithMultipleEvents_ShowsPluralCount()
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

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Contains("3 events", cut.Markup));
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

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() =>
        {
            var counts = cut.FindAll(".ttl-leg-count");
            Assert.Equal(2, counts.Count);
            Assert.Equal("1 event", counts[0].TextContent.Trim());
            Assert.Equal("0 events", counts[1].TextContent.Trim());
        });
    }

    [Fact]
    public void AddEventButton_EmitsSlotSelectionForThatLegWithLegStart()
    {
        var legStart = new DateTime(2026, 9, 1, 8, 0, 0);
        var leg = Leg("Paris", legStart, new DateTime(2026, 9, 2, 8, 0, 0));
        var response = Response(leg);

        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(response));
        TripTimeline.TimelineSlotSelection? captured = null;
        var cut = RenderComponent<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.OnLegSlotSelected, s => captured = s));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".ttl-leg-add")));
        cut.Find(".ttl-leg-add").Click();

        Assert.NotNull(captured);
        Assert.Equal(leg.TripLegId, captured!.TripLegId);
        Assert.Equal(legStart, captured.StartLocal);
    }

    [Fact]
    public void OverlappingEvents_StackOnSeparateLanes()
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

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() =>
        {
            var items = cut.FindAll(".ttl-item");
            Assert.Equal(2, items.Count);
            var tops = items
                .Select(i => i.GetAttribute("style"))
                .Select(ExtractTopRem)
                .ToList();
            // The two overlapping events must sit at different vertical offsets.
            Assert.NotEqual(tops[0], tops[1]);
        });
    }

    [Fact]
    public void OverlappingEvents_GrowLegRowHeight()
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

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() =>
        {
            var lane = cut.FindAll(".ttl-lane")[0];
            var minHeight = ExtractMinHeightRem(lane.GetAttribute("style"));
            // Two stacked lanes must reserve more than the default single-row height (3.75rem).
            Assert.True(minHeight > 3.75, $"Expected grown row, got {minHeight}rem.");
        });
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
    }
}
