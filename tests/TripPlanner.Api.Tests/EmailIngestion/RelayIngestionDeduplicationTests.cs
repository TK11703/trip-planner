using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// A relay that retries, or that delivers the same message twice, must not give the traveler a
/// second copy of the same drafts (US3). Genuinely different messages must both be processed.
/// </summary>
public sealed class RelayIngestionDeduplicationTests : IDisposable
{
    private readonly EmailIngestionApiFactory _factory = new();

    private HttpClient CreateRelayClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserHeader, "relay-application-id");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, EmailIngestionPolicy.RelayAppRole);
        return client;
    }

    private static async Task<IngestRelayMessageResponse> ReadAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<IngestRelayMessageResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;

    [Fact]
    public async Task ResubmittingTheSameMessageIdReportsADuplicateAndAddsNoDrafts()
    {
        var client = CreateRelayClient();
        var message = EmailIngestionApiFactory.SampleMessage(messageId: "message-retry-01");

        var first = await client.PostAsJsonAsync("/api/email-ingestion/messages", message);
        var second = await client.PostAsJsonAsync("/api/email-ingestion/messages", message);

        Assert.Equal(EmailIngestionOutcome.Parsed, (await ReadAsync(first)).Status);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var duplicate = await ReadAsync(second);
        Assert.Equal(EmailIngestionOutcome.Duplicate, duplicate.Status);
        Assert.Empty(duplicate.DraftIds);

        Assert.Single(_factory.Emails.Rows);
        Assert.Single(_factory.Drafts.Rows);
    }

    [Fact]
    public async Task MessagesWithoutAnIdAreDeduplicatedOnTheirContent()
    {
        var client = CreateRelayClient();
        var message = EmailIngestionApiFactory.SampleMessage(messageId: null);

        await client.PostAsJsonAsync("/api/email-ingestion/messages", message);
        var second = await client.PostAsJsonAsync("/api/email-ingestion/messages", message);

        Assert.Equal(EmailIngestionOutcome.Duplicate, (await ReadAsync(second)).Status);
        Assert.Single(_factory.Emails.Rows);
        Assert.Single(_factory.Drafts.Rows);
    }

    [Fact]
    public async Task DistinctMessagesSharingASenderAndSubjectAreBothProcessed()
    {
        var client = CreateRelayClient();

        var first = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(messageId: "leg-one", bodyText: "Outbound ABC123 departs 12 Aug 2026."));
        var second = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(messageId: "leg-two", bodyText: "Return XYZ789 departs 19 Aug 2026."));

        Assert.Equal(EmailIngestionOutcome.Parsed, (await ReadAsync(first)).Status);
        Assert.Equal(EmailIngestionOutcome.Parsed, (await ReadAsync(second)).Status);
        Assert.Equal(2, _factory.Emails.Rows.Count);
        Assert.Equal(2, _factory.Drafts.Rows.Count);
    }

    [Fact]
    public async Task TheSameMessageToTwoTravelersIsNotTreatedAsADuplicate()
    {
        _factory.Profiles.Add("second-traveler-id", "companion@contoso.com");
        var client = CreateRelayClient();

        var first = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(messageId: "shared-booking"));
        var second = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(messageId: "shared-booking", sender: "companion@contoso.com"));

        Assert.Equal(EmailIngestionOutcome.Parsed, (await ReadAsync(first)).Status);
        Assert.Equal(EmailIngestionOutcome.Parsed, (await ReadAsync(second)).Status);
        Assert.Equal(2, _factory.Emails.Rows.Count);
    }

    public void Dispose() => _factory.Dispose();
}
