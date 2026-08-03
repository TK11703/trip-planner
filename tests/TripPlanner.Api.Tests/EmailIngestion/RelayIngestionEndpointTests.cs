using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TripPlanner.Api.Features.EmailIngestion;
using TripPlanner.Api.Tests.Infrastructure;
using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Api.Tests.EmailIngestion;

/// <summary>
/// Covers every outcome the relay can receive from POST /api/email-ingestion/messages.
/// Each response must be conclusive: the relay never has to poll for a result (SC-002).
/// </summary>
public sealed class RelayIngestionEndpointTests : IDisposable
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
    public async Task RecognizedMessageProducesDraftsAndNotifiesTheOwningTraveler()
    {
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(EmailIngestionOutcome.Parsed, body.Status);
        Assert.NotNull(body.InboxEmailId);
        Assert.Single(body.DraftIds);

        // The traveler comes from the sender, never from the relay's own identity.
        var stored = Assert.Single(_factory.Emails.Rows);
        Assert.Equal(EmailIngestionApiFactory.TravelerUserId, stored.UserId);
        Assert.Equal("parsed", stored.ParseStatus);

        var draft = Assert.Single(_factory.Drafts.Rows);
        Assert.Equal("pending_review", draft.ReviewStatus);
        Assert.Single(_factory.Notifications.Created);
    }

    [Fact]
    public async Task MessageWithNothingRecognizableReportsNoContentAndCreatesNoDrafts()
    {
        _factory.Recognizer.Behavior = (_, _, _) => RecognitionResult.Unsupported();
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(bodyText: "Thanks for signing up for our newsletter."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(EmailIngestionOutcome.NoContent, body.Status);
        Assert.Empty(body.DraftIds);
        Assert.Equal("unsupported", Assert.Single(_factory.Emails.Rows).ParseStatus);
        Assert.Empty(_factory.Drafts.Rows);
        Assert.Empty(_factory.Notifications.Created);
    }

    [Fact]
    public async Task UnknownSenderIsRejectedAndNothingIsStored()
    {
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(sender: "stranger@example.com"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(EmailIngestionOutcome.UnknownSender, (await ReadAsync(response)).Status);
        Assert.Empty(_factory.Emails.Rows);
        Assert.Empty(_factory.Drafts.Rows);
    }

    [Fact]
    public async Task AmbiguousSenderIsRejectedRatherThanGuessed()
    {
        _factory.Profiles.Add("second-traveler-id", EmailIngestionApiFactory.TravelerEmail);
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(EmailIngestionOutcome.UnknownSender, (await ReadAsync(response)).Status);
        Assert.Empty(_factory.Emails.Rows);
    }

    [Fact]
    public async Task MissingSenderIsARequestError()
    {
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(sender: "  "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(EmailIngestionOutcome.InvalidRequest, (await ReadAsync(response)).Status);
        Assert.Empty(_factory.Emails.Rows);
    }

    [Fact]
    public async Task MalformedAttachmentEncodingIsARequestError()
    {
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(attachments:
            [
                new RelayEmailAttachment("itinerary.txt", "text/plain", "not-valid-base64!!")
            ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(EmailIngestionOutcome.InvalidRequest, (await ReadAsync(response)).Status);
        Assert.Empty(_factory.Emails.Rows);
    }

    [Fact]
    public async Task MessageWithNoUsableTextIsARequestError()
    {
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(subject: "", bodyText: null, bodyHtml: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(EmailIngestionOutcome.InvalidRequest, (await ReadAsync(response)).Status);
    }

    [Fact]
    public async Task OversizedAttachmentIsRejectedWithoutStoringTheMessage()
    {
        var client = CreateRelayClient();
        var oversized = Convert.ToBase64String(new byte[EmailAttachmentTextExtractor.MaxAttachmentBytes + 1024]);

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(attachments:
            [
                new RelayEmailAttachment("huge.bin", "application/octet-stream", oversized)
            ]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(EmailIngestionOutcome.TooLarge, (await ReadAsync(response)).Status);
        Assert.Empty(_factory.Emails.Rows);
    }

    [Fact]
    public async Task RecognitionOutageIsReportedAsRetryable()
    {
        _factory.Recognizer.Behavior = (_, _, _) => RecognitionResult.Failed();
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(EmailIngestionOutcome.ProcessingFailed, body.Status);
        Assert.Empty(body.DraftIds);
        Assert.Equal("failed", Assert.Single(_factory.Emails.Rows).ParseStatus);
        Assert.Empty(_factory.Drafts.Rows);
    }

    [Fact]
    public async Task AttachmentTextIsRetainedAndFedToRecognition()
    {
        var client = CreateRelayClient();

        var response = await client.PostAsJsonAsync("/api/email-ingestion/messages",
            EmailIngestionApiFactory.SampleMessage(attachments:
            [
                EmailIngestionApiFactory.TextAttachment("itinerary.txt", "text/plain", "Hotel Contoso, check-in 12 Aug 2026."),
                EmailIngestionApiFactory.TextAttachment("boarding.pdf", "application/pdf", "%PDF-1.4 binary-ish")
            ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _factory.Attachments.Rows.Count);

        // Text-based attachments contribute their text; binary formats are retained without it.
        var text = _factory.Attachments.Rows.Single(a => a.FileName == "itinerary.txt");
        Assert.Contains("Hotel Contoso", text.ExtractedText, StringComparison.Ordinal);
        Assert.Null(_factory.Attachments.Rows.Single(a => a.FileName == "boarding.pdf").ExtractedText);
        Assert.Contains("Hotel Contoso", _factory.Recognizer.LastAssembledText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IngestionNeverCreatesATripEvent()
    {
        var client = CreateRelayClient();

        await client.PostAsJsonAsync("/api/email-ingestion/messages", EmailIngestionApiFactory.SampleMessage());

        // Everything recognition produced is a draft awaiting review; nothing is on a timeline.
        Assert.All(_factory.Drafts.Rows, draft =>
        {
            Assert.Equal("pending_review", draft.ReviewStatus);
            Assert.Null(draft.TripId);
            Assert.Null(draft.TripLegId);
        });
    }

    public void Dispose() => _factory.Dispose();
}
