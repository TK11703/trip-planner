using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Timeline;

// User Story 2 (P2): Step between days quickly, bounded to the trip range.
public class TripTimelineStepTests : TestContext
{
    public TripTimelineStepTests()
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
    public async Task NextAndPrevious_MoveOneDay()
    {
        var cut = Render(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        await cut.InvokeAsync(() => cut.Instance.GoToNextDayAsync());
        Assert.Equal(new DateOnly(2026, 9, 2), cut.Instance.CurrentDate);

        await cut.InvokeAsync(() => cut.Instance.GoToNextDayAsync());
        Assert.Equal(new DateOnly(2026, 9, 3), cut.Instance.CurrentDate);

        await cut.InvokeAsync(() => cut.Instance.GoToPreviousDayAsync());
        Assert.Equal(new DateOnly(2026, 9, 2), cut.Instance.CurrentDate);
    }

    [Fact]
    public async Task Previous_ClampsAtTripStart()
    {
        var cut = Render(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));

        Assert.True(cut.Instance.AtTripStart);
        await cut.InvokeAsync(() => cut.Instance.GoToPreviousDayAsync());
        // Starts at trip start; previous is a no-op that never moves before the first day.
        Assert.True(cut.Instance.CurrentDate is null || cut.Instance.CurrentDate == new DateOnly(2026, 9, 1));
        Assert.True(cut.Instance.AtTripStart);
    }

    [Fact]
    public async Task Next_ClampsAtTripEnd()
    {
        var cut = Render(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));

        await cut.InvokeAsync(() => cut.Instance.GoToTripEndAsync());
        Assert.Equal(new DateOnly(2026, 9, 3), cut.Instance.CurrentDate);
        Assert.True(cut.Instance.AtTripEnd);

        await cut.InvokeAsync(() => cut.Instance.GoToNextDayAsync());
        Assert.Equal(new DateOnly(2026, 9, 3), cut.Instance.CurrentDate);
        Assert.True(cut.Instance.AtTripEnd);
    }

    [Fact]
    public async Task SingleDayTrip_ReachesBothBoundariesImmediately()
    {
        var cut = Render(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));

        Assert.True(cut.Instance.AtTripStart);
        Assert.True(cut.Instance.AtTripEnd);

        await cut.InvokeAsync(() => cut.Instance.GoToNextDayAsync());
        await cut.InvokeAsync(() => cut.Instance.GoToPreviousDayAsync());
        Assert.True(cut.Instance.AtTripStart);
        Assert.True(cut.Instance.AtTripEnd);
    }
}
