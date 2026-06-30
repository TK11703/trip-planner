using Xunit;

namespace TripPlanner.Api.Tests.Audit;

public class AuditEventTests
{
    [Fact(Skip = "Requires Testcontainers-backed Postgres to verify audit_events persistence.")]
    public void CrossUserAccess_RecordsAuditEvent_WithoutTokenOrSecret() { }
}
