using Xunit;

namespace TripPlanner.Api.Tests.Audit;

public class AuditMutationTests
{
    [Fact(Skip = "Requires Testcontainers-backed Postgres to read audit_events table.")]
    public void TripMutations_AppendAuditEvents() { }
}
