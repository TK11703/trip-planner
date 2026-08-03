using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Web.Components.Pages.EmailIngestion;
using TripPlanner.Web.Features.EmailIngestion;
using TripPlanner.Web.Features.InboxHistory;
using Xunit;

namespace TripPlanner.Web.Tests.EmailIngestion;

/// <summary>
/// The review surface survived the move to relayed ingestion (FR-024): travelers can still see
/// what arrived, review drafts, and act on them.
/// </summary>
public class InboxReviewPageTests : TestContext
{
    private static ParsedEventDraftDto Draft(Guid? tripId = null, Guid? legId = null, DateTime? start = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), tripId, legId, "flight", "Flight ABC123", "SEA",
            start, "America/Los_Angeles", null, null, "ABC123", null, 0.92,
            ReviewStatus.PendingReview, DateTimeOffset.UtcNow);

    [Fact]
    public void DraftsPageListsPendingEventsForReview()
    {
        var draft = Draft(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 12, 9, 30, 0));
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([draft]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Flight ABC123", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("ABC123", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Confirm", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Discard", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ADraftWithoutATripCannotBeConfirmedFromTheUi()
    {
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([Draft()]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Assign this event to a trip before confirming.", cut.Markup, StringComparison.Ordinal);
            var confirm = cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal));
            Assert.True(confirm.HasAttribute("disabled"));
        });
    }

    [Fact]
    public void DiscardingADraftRemovesItFromTheQueue()
    {
        var client = new StubEmailIngestionApiClient([Draft(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 12, 9, 30, 0))]);
        Services.AddSingleton<IEmailIngestionApiClient>(client);

        var cut = RenderComponent<InboxDrafts>();
        cut.WaitForAssertion(() => Assert.Contains("Flight ABC123", cut.Markup, StringComparison.Ordinal));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Discard", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("No events are waiting for review", cut.Markup, StringComparison.Ordinal));
        Assert.Single(client.Discarded);
    }

    [Fact]
    public void AnEmptyQueueExplainsHowMessagesArrive()
    {
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() => Assert.Contains("No events are waiting for review", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void InboxHistoryShowsWhatArrivedAndItsOutcome()
    {
        var parsed = new InboxEmailDto(Guid.NewGuid(), "traveler@contoso.com", "Flight confirmation", DateTimeOffset.UtcNow, ParseStatus.Parsed);
        var unsupported = new InboxEmailDto(Guid.NewGuid(), "traveler@contoso.com", "Newsletter", DateTimeOffset.UtcNow, ParseStatus.Unsupported);
        Services.AddSingleton<IInboxHistoryApiClient>(new StubInboxHistoryApiClient([parsed, unsupported]));

        var cut = RenderComponent<InboxHistory>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Flight confirmation", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Newsletter", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Parsed", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Unsupported", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void OnlyMessagesThatDidNotYieldEventsOfferReprocessing()
    {
        var parsed = new InboxEmailDto(Guid.NewGuid(), "traveler@contoso.com", "Flight confirmation", DateTimeOffset.UtcNow, ParseStatus.Parsed);
        var failed = new InboxEmailDto(Guid.NewGuid(), "traveler@contoso.com", "Hotel booking", DateTimeOffset.UtcNow, ParseStatus.Failed);
        var client = new StubInboxHistoryApiClient([parsed, failed]);
        Services.AddSingleton<IInboxHistoryApiClient>(client);

        var cut = RenderComponent<InboxHistory>();
        cut.WaitForAssertion(() => Assert.Contains("Hotel booking", cut.Markup, StringComparison.Ordinal));

        var reprocessButtons = cut.FindAll("button").Where(b => b.TextContent.Contains("Re-process", StringComparison.Ordinal)).ToList();
        Assert.Single(reprocessButtons);

        reprocessButtons[0].Click();
        cut.WaitForAssertion(() => Assert.Equal(failed.InboxEmailId, Assert.Single(client.Reprocessed)));
    }

    private sealed class StubEmailIngestionApiClient(IReadOnlyList<ParsedEventDraftDto> drafts) : IEmailIngestionApiClient
    {
        private readonly List<ParsedEventDraftDto> _drafts = [.. drafts];

        public List<Guid> Discarded { get; } = [];

        public Task<IReadOnlyList<ParsedEventDraftDto>> GetDraftsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ParsedEventDraftDto>>([.. _drafts]);

        public Task<ParsedEventDraftDto?> UpdateDraftAsync(Guid draftId, UpdateParsedEventDraftRequest request, CancellationToken ct = default)
            => Task.FromResult<ParsedEventDraftDto?>(null);

        public Task<ConfirmParsedEventDraftResponse?> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default)
        {
            _drafts.RemoveAll(d => d.ParsedEventDraftId == draftId);
            return Task.FromResult<ConfirmParsedEventDraftResponse?>(new ConfirmParsedEventDraftResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        }

        public Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default)
        {
            Discarded.Add(draftId);
            _drafts.RemoveAll(d => d.ParsedEventDraftId == draftId);
            return Task.FromResult(true);
        }
    }

    private sealed class StubInboxHistoryApiClient(IReadOnlyList<InboxEmailDto> items) : IInboxHistoryApiClient
    {
        public List<Guid> Reprocessed { get; } = [];

        public Task<IReadOnlyList<InboxEmailDto>> GetHistoryAsync(CancellationToken ct = default)
            => Task.FromResult(items);

        public Task<bool> ReprocessEmailAsync(Guid inboxEmailId, CancellationToken ct = default)
        {
            Reprocessed.Add(inboxEmailId);
            return Task.FromResult(true);
        }
    }
}
