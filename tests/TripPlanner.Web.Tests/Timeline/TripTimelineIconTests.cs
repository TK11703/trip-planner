using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Timeline;

// User Story 2 (P2): Timeline items render a type icon that reflects their type.
public class TripTimelineIconTests : TestContext
{
    public TripTimelineIconTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static TimelineItem Item(Guid legId, string title, string itemType, DateTime start)
        => new(
            Guid.NewGuid(),
            legId,
            itemType,
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

    private static TripTimelineResponse ResponseWith(params TimelineItem[] items)
    {
        var legId = Guid.NewGuid();
        var leg = new TimelineLeg(legId, "Paris", null, null,
            new DateTime(2026, 9, 1, 8, 0, 0), "UTC", "UTC",
            new DateTime(2026, 9, 2, 8, 0, 0), "UTC", "UTC", 0,
            items.Select(i => i with { TripLegId = legId }).ToArray());
        return new TripTimelineResponse(Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), 30,
            new[] { leg }, Array.Empty<TimelineItem>());
    }

    [Fact]
    public void TimelineItems_RenderTypeIcon()
    {
        var legId = Guid.NewGuid();
        var response = ResponseWith(
            Item(legId, "Dinner", "reservation", new DateTime(2026, 9, 1, 19, 0, 0)),
            Item(legId, "Museum", "activity", new DateTime(2026, 9, 1, 10, 0, 0)));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ttl-item .ttl-item-type-icon").Count));
    }

    [Fact]
    public void DifferentTypes_RenderDistinctGlyphs()
    {
        var legId = Guid.NewGuid();
        var response = ResponseWith(
            Item(legId, "Reminder", "reminder", new DateTime(2026, 9, 1, 9, 0, 0)),
            Item(legId, "Event", "event", new DateTime(2026, 9, 1, 12, 0, 0)));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ttl-item .ttl-item-type-icon").Count));
        var glyphs = cut.FindAll(".ttl-item .ttl-item-type-icon").Select(svg => svg.InnerHtml).ToArray();

        Assert.NotEqual(glyphs[0], glyphs[1]);
    }
}
