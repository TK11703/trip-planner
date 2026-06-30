using Xunit;

namespace TripPlanner.Database.Tests.Timeline;

[Trait("Category", "DatabaseIntegration")]
public class TimelineQueryTests
{
    [Fact(Skip = "Requires Docker/Testcontainers.")]
    public void Timeline_OnlyIncludesOwnerScopedEvents() { }
}
