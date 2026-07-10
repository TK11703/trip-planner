using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Timeline;

// User Story 1 (P1): Track the active (centered) date reported from scrolling.
public class TripTimelineActiveDateTests : TestContext
{
    public TripTimelineActiveDateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task SetCenteredDayIndex_UpdatesCurrentDate_AndRaisesActiveDateChanged()
    {
        var response = TimelineNavTestData.Response(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        DateOnly? raised = null;
        var cut = RenderComponent<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.OnActiveDateChanged, d => raised = d));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Instance.TripDates));

        await cut.InvokeAsync(() => cut.Instance.SetCenteredDayIndex(4));

        Assert.Equal(new DateOnly(2026, 9, 5), cut.Instance.CurrentDate);
        Assert.Equal(new DateOnly(2026, 9, 5), raised);
    }

    [Fact]
    public async Task SetCenteredDayIndex_ClampsToTripRange()
    {
        var response = TimelineNavTestData.Response(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Instance.TripDates));

        await cut.InvokeAsync(() => cut.Instance.SetCenteredDayIndex(99));

        Assert.Equal(new DateOnly(2026, 9, 3), cut.Instance.CurrentDate);
    }

    [Fact]
    public async Task SetCenteredDayIndex_SameDay_DoesNotRaiseAgain()
    {
        var response = TimelineNavTestData.Response(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var raisedCount = 0;
        var cut = RenderComponent<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.OnActiveDateChanged, _ => raisedCount++));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Instance.TripDates));

        await cut.InvokeAsync(() => cut.Instance.SetCenteredDayIndex(2));
        await cut.InvokeAsync(() => cut.Instance.SetCenteredDayIndex(2));

        Assert.Equal(1, raisedCount);
        Assert.Equal(new DateOnly(2026, 9, 3), cut.Instance.CurrentDate);
    }
}
