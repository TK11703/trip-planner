using Xunit;

namespace TripPlanner.Api.Tests.Timeline;

public class TimelineEndpointTests
{
    [Fact(Skip = "Requires DB-backed integration.")]
    public void GetTimeline_ReturnsOwnerScopedEvents() { }
}
