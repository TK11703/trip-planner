using System.Net;
using System.Net.Http.Json;
using Dapper;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Trips;
using Xunit;

namespace TripPlanner.Api.Tests.Audit;

/// <summary>
/// The audit trail is the record of who tried what. It only has value if the rows actually reach
/// the table, so these read <c>audit_events</c> back rather than asserting against a recording
/// fake — a fake would confirm the call was made, not that anything was stored.
///
/// The second obligation is what the rows must <em>not</em> contain. An audit trail that copies
/// credentials turns a useful log into a liability, so the persisted text is checked for
/// anything resembling a token.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(PostgresApiFactory.ApiPostgresCollectionName)]
public class AuditEventTests
{
    private readonly PostgresApiFactory _factory;

    public AuditEventTests(PostgresApiFactory factory) => _factory = factory;

    private const string SecretBearerToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.super-secret-do-not-log";

    private HttpClient ClientFor(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestScopeHeader, "access_as_user");
        return client;
    }

    private static string NewUser() => $"user-{Guid.NewGuid():N}";

    private async Task<Guid> SeedTripAsync(string ownerUserId, string name = "Audited trip")
    {
        var tripId = Guid.NewGuid();
        await using var conn = await _factory.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO trips (trip_id, owner_user_id, name, start_date, end_date)
            VALUES (@tripId, @ownerUserId, @name, DATE '2026-07-10', DATE '2026-07-20');
            """,
            new { tripId, ownerUserId, name });
        return tripId;
    }

    private async Task<IReadOnlyList<(string? UserId, string Operation, string ResourceType, string? ResourceId, string Result)>>
        ReadEventsAsync(string userId)
    {
        await using var conn = await _factory.OpenAsync();
        var rows = await conn.QueryAsync<(string? UserId, string Operation, string ResourceType, string? ResourceId, string Result)>(
            """
            SELECT user_id AS "UserId", operation AS "Operation", resource_type AS "ResourceType",
                   resource_id AS "ResourceId", result AS "Result"
            FROM audit_events
            WHERE user_id = @userId
            ORDER BY occurred_at_utc
            """,
            new { userId });
        return rows.ToArray();
    }

    [Fact]
    public async Task CrossUserAccess_RecordsAuditEvent_WithoutTokenOrSecret()
    {
        var owner = NewUser();
        var intruder = NewUser();
        var tripId = await SeedTripAsync(owner);

        var client = ClientFor(intruder);
        // A credential on the request is exactly what must not end up in the log.
        client.DefaultRequestHeaders.Authorization = new("Bearer", SecretBearerToken);

        var response = await client.GetAsync($"/api/trips/{tripId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var events = await ReadEventsAsync(intruder);
        var denial = Assert.Single(events);
        Assert.Equal(AuditOperations.AccessDenied, denial.Operation);
        Assert.Equal(AuditResults.Denied, denial.Result);
        Assert.Equal("trip", denial.ResourceType);
        Assert.Equal(tripId.ToString(), denial.ResourceId);

        // Nothing credential-shaped reached the table, in this row or any other.
        await using var conn = await _factory.OpenAsync();
        var everything = await conn.ExecuteScalarAsync<string>(
            """
            SELECT coalesce(string_agg(
                coalesce(user_id,'') || '|' || operation || '|' || resource_type || '|' ||
                coalesce(resource_id,'') || '|' || result, ' '), '')
            FROM audit_events
            """);

        Assert.DoesNotContain("Bearer", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SecretBearerToken, everything, StringComparison.Ordinal);
        Assert.DoesNotContain("eyJ", everything, StringComparison.Ordinal);
    }

    /// <summary>
    /// A successful read is recorded too — the trail answers "who saw this", not only "who was
    /// turned away".
    /// </summary>
    [Fact]
    public async Task ReadingOwnTrip_RecordsASuccessfulReadEvent()
    {
        var owner = NewUser();
        var tripId = await SeedTripAsync(owner);

        var response = await ClientFor(owner).GetAsync($"/api/trips/{tripId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var events = await ReadEventsAsync(owner);
        Assert.Contains(events, e =>
            e.Operation == AuditOperations.TripRead &&
            e.Result == AuditResults.Success &&
            e.ResourceId == tripId.ToString());
    }

    /// <summary>The row is attributed to the caller who tried, not to the trip's owner.</summary>
    [Fact]
    public async Task ADenialIsAttributedToTheCallerNotTheOwner()
    {
        var owner = NewUser();
        var intruder = NewUser();
        var tripId = await SeedTripAsync(owner);

        await ClientFor(intruder).GetAsync($"/api/trips/{tripId}");

        Assert.Empty(await ReadEventsAsync(owner));
        Assert.Single(await ReadEventsAsync(intruder));
    }
}

/// <summary>
/// Every mutation leaves a trace, including the ones that were refused. Read back from the table
/// so a change that stops writing audit rows fails here rather than silently.
/// </summary>
[Trait("Category", "DatabaseIntegration")]
[Collection(PostgresApiFactory.ApiPostgresCollectionName)]
public class AuditMutationTests
{
    private readonly PostgresApiFactory _factory;

    public AuditMutationTests(PostgresApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(string userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, userId);
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestScopeHeader, "access_as_user");
        return client;
    }

    private static string NewUser() => $"user-{Guid.NewGuid():N}";

    private async Task<string[]> OperationsForAsync(string userId)
    {
        await using var conn = await _factory.OpenAsync();
        var rows = await conn.QueryAsync<string>(
            "SELECT operation FROM audit_events WHERE user_id = @userId ORDER BY occurred_at_utc",
            new { userId });
        return rows.ToArray();
    }

    [Fact]
    public async Task TripMutations_AppendAuditEvents()
    {
        var owner = NewUser();
        var client = ClientFor(owner);

        var created = await client.PostAsJsonAsync("/api/trips",
            new CreateTripRequest("Audited", null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var tripId = (await created.Content.ReadFromJsonAsync<CreateTripResponse>())!.TripId;

        // Update answers with the updated trip; delete answers with nothing.
        var updated = await client.PutAsJsonAsync($"/api/trips/{tripId}",
            new UpdateTripRequest("Audited again", null, new DateOnly(2026, 7, 11), new DateOnly(2026, 7, 19)));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var deleted = await client.DeleteAsync($"/api/trips/{tripId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var operations = await OperationsForAsync(owner);
        Assert.Contains(AuditOperations.TripCreate, operations);
        Assert.Contains(AuditOperations.TripUpdate, operations);
        Assert.Contains(AuditOperations.TripDelete, operations);
    }

    /// <summary>
    /// A refused mutation is the more interesting case: the trip is untouched, but the attempt
    /// still has to be on record.
    /// </summary>
    [Fact]
    public async Task ARefusedMutation_IsStillRecorded()
    {
        var owner = NewUser();
        var intruder = NewUser();

        var created = await ClientFor(owner).PostAsJsonAsync("/api/trips",
            new CreateTripRequest("Not yours", null, new DateOnly(2026, 7, 10), new DateOnly(2026, 7, 20)));
        var tripId = (await created.Content.ReadFromJsonAsync<CreateTripResponse>())!.TripId;

        var refused = await ClientFor(intruder).DeleteAsync($"/api/trips/{tripId}");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        Assert.Contains(AuditOperations.AccessDenied, await OperationsForAsync(intruder));

        await using var conn = await _factory.OpenAsync();
        Assert.Equal(1, await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM trips WHERE trip_id = @tripId", new { tripId }));
    }
}
