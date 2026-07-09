using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Web.Components.Timeline;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Timeline;

// User Story 1 (P1): Jump directly to a chosen date.
public class TripTimelineJumpTests : TestContext
{
    public TripTimelineJumpTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void TripDates_ContainsOneEntryPerDay_Inclusive()
    {
        var response = TimelineNavTestData.Response(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));

        cut.WaitForAssertion(() => Assert.Equal(10, cut.Instance.TripDates.Count));
        Assert.Equal(new DateOnly(2026, 9, 1), cut.Instance.TripDates[0]);
        Assert.Equal(new DateOnly(2026, 9, 10), cut.Instance.TripDates[^1]);
    }

    [Fact]
    public async Task ScrollToDateAsync_SetsCurrentDate_ClampedToRange()
    {
        var response = TimelineNavTestData.Response(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var cut = RenderComponent<TripTimeline>(p => p.Add(x => x.TripId, response.TripId));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Instance.TripDates));

        await cut.InvokeAsync(() => cut.Instance.ScrollToDateAsync(new DateOnly(2026, 9, 5)));
        Assert.Equal(new DateOnly(2026, 9, 5), cut.Instance.CurrentDate);

        // Out-of-range requests clamp to the trip boundary rather than scrolling into empty space.
        await cut.InvokeAsync(() => cut.Instance.ScrollToDateAsync(new DateOnly(2026, 12, 1)));
        Assert.Equal(new DateOnly(2026, 9, 10), cut.Instance.CurrentDate);
    }

    [Fact]
    public void AddLegFooter_Rendered_OnlyWhenEditable()
    {
        var response = TimelineNavTestData.Response(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var editable = RenderComponent<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.CanEdit, true));
        editable.WaitForAssertion(() => Assert.Contains("ttl-leg-add-footer", editable.Markup));

        var readOnly = RenderComponent<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.CanEdit, false));
        readOnly.WaitForAssertion(() => Assert.NotEmpty(readOnly.Instance.TripDates));
        Assert.DoesNotContain("ttl-leg-add-footer", readOnly.Markup);
    }

    [Fact]
    public async Task AddLegFooter_InvokesOnAddLegRequested()
    {
        var response = TimelineNavTestData.Response(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));
        Services.AddSingleton<ITripApiClient>(new NavStubTripApiClient(response));

        var invoked = false;
        var cut = RenderComponent<TripTimeline>(p => p
            .Add(x => x.TripId, response.TripId)
            .Add(x => x.CanEdit, true)
            .Add(x => x.OnAddLegRequested, () => invoked = true));

        cut.WaitForAssertion(() => Assert.Contains("ttl-leg-add-footer", cut.Markup));
        await cut.InvokeAsync(() => cut.Find(".ttl-leg-add-footer").Click());

        Assert.True(invoked);
    }
}
