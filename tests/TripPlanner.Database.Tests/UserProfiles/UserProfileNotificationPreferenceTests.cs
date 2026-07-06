using TripPlanner.Database.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Database.Tests.UserProfiles;

[Trait("Category", "DatabaseIntegration")]
public class UserProfileNotificationPreferenceTests : IClassFixture<PostgresFixture>
{
    [Fact(Skip = "Requires Docker/Testcontainers.")]
    public void UpdateAsync_PersistsNotificationPreferences() { }
}
