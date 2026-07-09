using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Timeline;

// User Story 3 (P3): Orient to today and trip boundaries.
public class TripTimelineShortcutTests : TestContext
{
    public TripTimelineShortcutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<TripTimeline> Render(DateOnly start, DateOnly end)
    {
        var response = TimelineNavTestData.Response(start, end);
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));
        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Instance.TripDates));
        return cut;
    }

    [Fact]
    public async Task TripStartAndEnd_JumpToBoundaries()
    {
        var cut = Render(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        await cut.InvokeAsync(() => cut.Instance.GoToTripEndAsync());
        Assert.Equal(new DateOnly(2026, 9, 10), cut.Instance.CurrentDate);

        await cut.InvokeAsync(() => cut.Instance.GoToTripStartAsync());
        Assert.Equal(new DateOnly(2026, 9, 1), cut.Instance.CurrentDate);
    }

    [Fact]
    public void TodayInRange_FalseWhenTodayOutsideTrip()
    {
        // A fixed historical range that cannot contain the current date.
        var cut = Render(new DateOnly(2000, 1, 1), new DateOnly(2000, 1, 5));

        Assert.False(cut.Instance.TodayInRange);
    }

    [Fact]
    public async Task GoToTodayAsync_NoOp_WhenOutsideRange()
    {
        var cut = Render(new DateOnly(2000, 1, 1), new DateOnly(2000, 1, 5));

        await cut.InvokeAsync(() => cut.Instance.GoToTodayAsync());
        Assert.Null(cut.Instance.CurrentDate);
    }

    [Fact]
    public async Task GoToTodayAsync_ScrollsToToday_WhenInRange()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var cut = Render(today.AddDays(-2), today.AddDays(2));

        Assert.True(cut.Instance.TodayInRange);
        await cut.InvokeAsync(() => cut.Instance.GoToTodayAsync());
        Assert.Equal(today, cut.Instance.CurrentDate);
    }
}
