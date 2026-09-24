using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages.EmailIngestion;
using TripPlanner.Web.Features.EmailIngestion;
using TripPlanner.Web.Features.InboxHistory;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Web.Tests.EmailIngestion;

/// <summary>
/// The review surface survived the move to relayed ingestion (FR-024): travelers can still see
/// what arrived, review drafts, and act on them.
/// </summary>
public class InboxReviewPageTests : TestContext
{
    private static ParsedItemDraftDto Draft(Guid? tripId = null, Guid? legId = null, DateTime? start = null, DraftPlacement? placement = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), tripId, legId, "flight", "Flight ABC123", "SEA",
            start, "America/Los_Angeles", null, null, "ABC123", null, 0.92,
            ReviewStatus.PendingReview, DateTimeOffset.UtcNow, placement);

    private static PlacementCandidate Candidate(string tripName, string legTitle, int startDay, int endDay) =>
        new(Guid.NewGuid(), tripName, Guid.NewGuid(), legTitle,
            new DateTimeOffset(2026, 8, startDay, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, endDay, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void DraftsPageListsPendingItemsForReview()
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
            Assert.Contains("Assign this item to a trip before confirming.", cut.Markup, StringComparison.Ordinal);
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

        cut.WaitForAssertion(() => Assert.Contains("No items are waiting for review", cut.Markup, StringComparison.Ordinal));
        Assert.Single(client.Discarded);
    }

    [Fact]
    public void AnEmptyQueueExplainsHowMessagesArrive()
    {
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() => Assert.Contains("No items are waiting for review", cut.Markup, StringComparison.Ordinal));
    }

    // Feature 024: the review queue proposes a home for each draft, but the traveler always has
    // the last word — nothing reaches the timeline without a Confirm click (FR-004 to FR-006).
    [Fact]
    public void AnUnambiguousMatchArrivesPreSelectedAndReadyToConfirm()
    {
        var candidate = Candidate("West Coast, August", "San Francisco", 12, 16);
        var draft = Draft(start: new DateTime(2026, 8, 13, 15, 0, 0), placement: new DraftPlacement(
            DraftPlacementStatus.Matched, candidate.TripId, candidate.TripLegId, [candidate]));
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([draft]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("West Coast, August", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("San Francisco", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Aug 12", cut.Markup, StringComparison.Ordinal);
            var confirm = cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal));
            Assert.False(confirm.HasAttribute("disabled"));
        });
    }

    [Fact]
    public void ConfirmingAMatchedDraftWritesThePlacementTheTravelerSaw()
    {
        var candidate = Candidate("West Coast, August", "San Francisco", 12, 16);
        var draft = Draft(start: new DateTime(2026, 8, 13, 15, 0, 0), placement: new DraftPlacement(
            DraftPlacementStatus.Matched, candidate.TripId, candidate.TripLegId, [candidate]));
        var client = new StubEmailIngestionApiClient([draft]);
        Services.AddSingleton<IEmailIngestionApiClient>(client);

        var cut = RenderComponent<InboxDrafts>();
        cut.WaitForAssertion(() => Assert.Contains("San Francisco", cut.Markup, StringComparison.Ordinal));

        Assert.Empty(client.Updated);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            var (draftId, request) = Assert.Single(client.Updated);
            Assert.Equal(draft.ParsedItemDraftId, draftId);
            Assert.Equal(candidate.TripId, request.TripId);
            Assert.Equal(candidate.TripLegId, request.TripLegId);
        });
    }

    [Fact]
    public void AnAmbiguousMatchLeavesTheChoiceToTheTraveler()
    {
        var first = Candidate("West Coast, August", "San Francisco", 12, 16);
        var second = Candidate("West Coast, August", "Napa", 13, 15);
        var draft = Draft(start: new DateTime(2026, 8, 14, 15, 0, 0), placement: new DraftPlacement(
            DraftPlacementStatus.Ambiguous, null, null, [first, second]));
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([draft]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("More than one trip leg covers these dates", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Napa", cut.Markup, StringComparison.Ordinal);
            var confirm = cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal));
            Assert.True(confirm.HasAttribute("disabled"));
        });
    }

    [Fact]
    public void ChoosingALegForAnAmbiguousDraftUnlocksConfirm()
    {
        var first = Candidate("West Coast, August", "San Francisco", 12, 16);
        var second = Candidate("West Coast, August", "Napa", 13, 15);
        var draft = Draft(start: new DateTime(2026, 8, 14, 15, 0, 0), placement: new DraftPlacement(
            DraftPlacementStatus.Ambiguous, null, null, [first, second]));
        var client = new StubEmailIngestionApiClient([draft]);
        Services.AddSingleton<IEmailIngestionApiClient>(client);

        var cut = RenderComponent<InboxDrafts>();
        cut.WaitForAssertion(() => Assert.Contains("Napa", cut.Markup, StringComparison.Ordinal));

        cut.Find("select").Change(second.TripLegId.ToString());

        var confirm = cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal));
        Assert.False(confirm.HasAttribute("disabled"));

        confirm.Click();

        cut.WaitForAssertion(() =>
        {
            var (_, request) = Assert.Single(client.Updated);
            Assert.Equal(second.TripLegId, request.TripLegId);
        });
    }

    [Fact]
    public void ADraftWithNothingToMatchOnSaysWhatIsMissing()
    {
        var draft = Draft(placement: new DraftPlacement(DraftPlacementStatus.InsufficientData, null, null, []));
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([draft]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("we can't tell", cut.Markup, StringComparison.Ordinal);
            var confirm = cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal));
            Assert.True(confirm.HasAttribute("disabled"));
        });
    }

    // FR-009 / FR-010: the two gap outcomes read differently, because the traveler's next move
    // differs — add a leg, versus check the dates.
    [Fact]
    public void AGapBetweenLegsAndAnUnplannedDateReadDifferently()
    {
        var inAGap = Draft(start: new DateTime(2026, 8, 17, 15, 0, 0), placement:
            new DraftPlacement(DraftPlacementStatus.NoLegCovers, null, null, []));
        var unplanned = Draft(start: new DateTime(2027, 4, 2, 15, 0, 0), placement:
            new DraftPlacement(DraftPlacementStatus.OutsideTripDates, null, null, []));
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([inAGap, unplanned]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() =>
        {
            var gap = Squash(cut.Find("[data-placement='no-leg-covers']").TextContent);
            Assert.Contains("Aug 17, 2026", gap, StringComparison.Ordinal);
            Assert.Contains("no leg of that trip covers it", gap, StringComparison.Ordinal);

            var outside = Squash(cut.Find("[data-placement='outside-trip-dates']").TextContent);
            Assert.Contains("Apr 2, 2027", outside, StringComparison.Ordinal);
            Assert.Contains("outside every trip you can edit", outside, StringComparison.Ordinal);
        });
    }

    /// <summary>Razor markup wraps and indents; compare on the words the traveler actually reads.</summary>
    private static string Squash(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

    // FR-011: a trip is enough to confirm; the leg can come later.
    [Fact]
    public void ADraftOnATripWithNoLegCanStillBeConfirmed()
    {
        var draft = Draft(Guid.NewGuid(), start: new DateTime(2026, 8, 17, 15, 0, 0), placement:
            new DraftPlacement(DraftPlacementStatus.NoLegCovers, null, null, []));
        Services.AddSingleton<IEmailIngestionApiClient>(new StubEmailIngestionApiClient([draft]));

        var cut = RenderComponent<InboxDrafts>();

        cut.WaitForAssertion(() =>
        {
            var confirm = cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal));
            Assert.False(confirm.HasAttribute("disabled"));
        });
    }

    // ---- Correcting a wrong placement before confirming (US3) -----------------------------

    private static TripSummary Trip(Guid tripId, string name, TripAccessLevel access = TripAccessLevel.Owner) =>
        new(tripId, name, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20),
            DateTimeOffset.UtcNow, 0, access, access == TripAccessLevel.Owner);

    private static TripDetail Detail(Guid tripId, string name, params TripLegDto[] legs) =>
        new(tripId, name, null, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, legs, Array.Empty<TrackedItemDto>());

    private static TripLegDto Leg(Guid tripId, string title, int startDay, int endDay, int sortOrder = 0,
        string? legKind = null, string? transportationMode = null) =>
        new(Guid.NewGuid(), tripId, title, null, null,
            new DateTime(2026, 8, startDay, 0, 0, 0), "UTC", null,
            new DateTime(2026, 8, endDay, 23, 59, 0), "UTC", null, null, sortOrder,
            legKind, transportationMode);

    private (IRenderedComponent<InboxDrafts> Cut, StubEmailIngestionApiClient Ingestion, StubTripApiClient Trips)
        RenderQueueWithEditableTrips(ParsedItemDraftDto draft, params (TripSummary Summary, TripDetail Detail)[] trips)
    {
        var ingestion = new StubEmailIngestionApiClient([draft]);
        var tripClient = new StubTripApiClient([.. trips.Select(t => t.Summary)]);
        foreach (var (summary, detail) in trips)
        {
            tripClient.Details[summary.TripId] = detail;
        }

        Services.AddSingleton<IEmailIngestionApiClient>(ingestion);
        Services.AddSingleton<ITripApiClient>(tripClient);
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());

        var cut = RenderComponent<InboxDrafts>();
        cut.WaitForAssertion(() => Assert.Contains("Flight ABC123", cut.Markup, StringComparison.Ordinal));
        return (cut, ingestion, tripClient);
    }

    private static void OpenEditModal(IRenderedComponent<InboxDrafts> cut)
    {
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Edit").Click();
        cut.WaitForAssertion(() => Assert.Contains("Edit parsed item", cut.Markup, StringComparison.Ordinal));
    }

    // FR-016: the traveler can reopen a draft and take the placement decision themselves.
    [Fact]
    public void TheEditModalOpensFromADraft()
    {
        var tripId = Guid.NewGuid();
        var draft = Draft(tripId, start: new DateTime(2026, 8, 12, 9, 30, 0));
        var (cut, _, _) = RenderQueueWithEditableTrips(draft,
            (Trip(tripId, "West Coast"), Detail(tripId, "West Coast", Leg(tripId, "San Francisco", 11, 14))));

        OpenEditModal(cut);

        Assert.Contains("Flight ABC123", cut.Find("#draft-title").GetAttribute("value")!, StringComparison.Ordinal);
        Assert.Contains("San Francisco", cut.Find("#draft-leg").InnerHtml, StringComparison.Ordinal);
    }

    // FR-018: a leg belonging to the trip the traveler just left would send the item somewhere
    // they did not choose, so the leg list is rebuilt and the stale choice dropped.
    [Fact]
    public void ChangingTheTripRepopulatesTheLegListAndClearsThePriorLeg()
    {
        var westId = Guid.NewGuid();
        var eastId = Guid.NewGuid();
        var westLeg = Leg(westId, "San Francisco", 11, 14);
        var eastLeg = Leg(eastId, "Boston", 11, 14);
        var draft = Draft(westId, westLeg.TripLegId, new DateTime(2026, 8, 12, 9, 30, 0));

        var (cut, _, _) = RenderQueueWithEditableTrips(draft,
            (Trip(westId, "West Coast"), Detail(westId, "West Coast", westLeg)),
            (Trip(eastId, "East Coast"), Detail(eastId, "East Coast", eastLeg)));

        OpenEditModal(cut);
        cut.WaitForAssertion(() => Assert.Equal(westLeg.TripLegId.ToString(), cut.Find("#draft-leg").GetAttribute("value")));

        cut.Find("#draft-trip").Change(eastId.ToString());

        cut.WaitForAssertion(() =>
        {
            var legSelect = cut.Find("#draft-leg");
            Assert.Contains("Boston", legSelect.InnerHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("San Francisco", legSelect.InnerHtml, StringComparison.Ordinal);
            Assert.True(string.IsNullOrEmpty(legSelect.GetAttribute("value")));
        });
    }

    // FR-003: a trip the traveler can only view is not a placement they could complete, so it
    // never appears in the picker.
    [Fact]
    public void AViewerLevelTripIsAbsentFromTheTripPicker()
    {
        var ownedId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var draft = Draft(ownedId, start: new DateTime(2026, 8, 12, 9, 30, 0));

        var (cut, _, _) = RenderQueueWithEditableTrips(draft,
            (Trip(ownedId, "West Coast"), Detail(ownedId, "West Coast", Leg(ownedId, "San Francisco", 11, 14))),
            (Trip(viewerId, "Someone Else's Trip", TripAccessLevel.Viewer), Detail(viewerId, "Someone Else's Trip")));

        OpenEditModal(cut);

        var tripSelect = cut.Find("#draft-trip");
        Assert.Contains("West Coast", tripSelect.InnerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Someone Else's Trip", tripSelect.InnerHtml, StringComparison.Ordinal);
    }

    // FR-017: every parsed field is editable, and Save persists what the traveler entered.
    [Fact]
    public void SavingTheModalPersistsTheEditedFields()
    {
        var tripId = Guid.NewGuid();
        var leg = Leg(tripId, "San Francisco", 11, 14);
        var draft = Draft(tripId, start: new DateTime(2026, 8, 12, 9, 30, 0));
        var (cut, ingestion, _) = RenderQueueWithEditableTrips(draft,
            (Trip(tripId, "West Coast"), Detail(tripId, "West Coast", leg)));

        OpenEditModal(cut);
        cut.Find("#draft-title").Change("Flight ABC123 (corrected)");
        cut.Find("#draft-leg").Change(leg.TripLegId.ToString());
        cut.Find("#draft-start-tz").Input("UTC");
        cut.Find(".tp-option-list button").Click();
        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var (_, request) = Assert.Single(ingestion.Updated);
            Assert.Equal("Flight ABC123 (corrected)", request.Title);
            Assert.Equal(tripId, request.TripId);
            Assert.Equal(leg.TripLegId, request.TripLegId);
            Assert.Equal("UTC", request.StartTimeZoneId);
        });
        cut.WaitForAssertion(() => Assert.DoesNotContain("Edit parsed item", cut.Markup, StringComparison.Ordinal));
    }

    // FR-022: a refusal is shown against the input the API named, so the traveler can fix it
    // without leaving the queue.
    [Fact]
    public void ARefusedConfirmationIsShownAgainstTheFieldItNames()
    {
        var tripId = Guid.NewGuid();
        var draft = Draft(tripId, Guid.NewGuid(), new DateTime(2026, 8, 12, 9, 30, 0));
        var ingestion = new StubEmailIngestionApiClient([draft])
        {
            NextError = ApiError.ValidationFailed("Start must fall within the trip leg.", "startLocal")
        };
        Services.AddSingleton<IEmailIngestionApiClient>(ingestion);

        var cut = RenderComponent<InboxDrafts>();
        cut.WaitForAssertion(() => Assert.Contains("Flight ABC123", cut.Markup, StringComparison.Ordinal));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Confirm", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() =>
        {
            var error = cut.Find($"[data-draft-error='{draft.ParsedItemDraftId}']");
            Assert.Contains("Start:", error.TextContent, StringComparison.Ordinal);
            Assert.Contains("Start must fall within the trip leg.", error.TextContent, StringComparison.Ordinal);
        });
        // The draft is still in the queue, because nothing was written.
        Assert.Contains("Flight ABC123", cut.Markup, StringComparison.Ordinal);
    }

    // ---- Feature 025: the draft's leg picker only offers legs that can hold an item --------

    [Theory]
    [InlineData(TransportationModes.Flight)]
    [InlineData(TransportationModes.Train)]
    [InlineData(TransportationModes.Bus)]
    [InlineData(TransportationModes.Boat)]
    public void RestrictedLegsAreAbsentFromTheDraftLegPicker(string mode)
    {
        var tripId = Guid.NewGuid();
        var stay = Leg(tripId, "San Francisco", 11, 14, legKind: TripLegKinds.Stay);
        var restricted = Leg(tripId, "Getting there", 11, 14, sortOrder: 1, legKind: TripLegKinds.Travel, transportationMode: mode);
        var draft = Draft(tripId, start: new DateTime(2026, 8, 12, 9, 30, 0));

        var (cut, _, _) = RenderQueueWithEditableTrips(draft,
            (Trip(tripId, "West Coast"), Detail(tripId, "West Coast", stay, restricted)));

        OpenEditModal(cut);

        var legSelect = cut.Find("#draft-leg");
        Assert.Contains("San Francisco", legSelect.InnerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Getting there", legSelect.InnerHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void CarLegsRemainAvailableInTheDraftLegPicker()
    {
        var tripId = Guid.NewGuid();
        var car = Leg(tripId, "Road trip", 11, 14, legKind: TripLegKinds.Travel, transportationMode: TransportationModes.Car);
        var draft = Draft(tripId, start: new DateTime(2026, 8, 12, 9, 30, 0));

        var (cut, _, _) = RenderQueueWithEditableTrips(draft,
            (Trip(tripId, "West Coast"), Detail(tripId, "West Coast", car)));

        OpenEditModal(cut);

        Assert.Contains("Road trip", cut.Find("#draft-leg").InnerHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// A trip made only of flights offers no leg at all, and the draft can still be saved
    /// unassigned rather than being stuck in the queue.
    /// </summary>
    [Fact]
    public void ATripOfOnlyRestrictedLegsLeavesTheDraftUnassigned()
    {
        var tripId = Guid.NewGuid();
        var outbound = Leg(tripId, "Outbound flight", 11, 12, legKind: TripLegKinds.Travel, transportationMode: TransportationModes.Flight);
        var inbound = Leg(tripId, "Return flight", 13, 14, sortOrder: 1, legKind: TripLegKinds.Travel, transportationMode: TransportationModes.Flight);
        var draft = Draft(tripId, start: new DateTime(2026, 8, 12, 9, 30, 0));

        var (cut, ingestion, _) = RenderQueueWithEditableTrips(draft,
            (Trip(tripId, "West Coast"), Detail(tripId, "West Coast", outbound, inbound)));

        OpenEditModal(cut);

        var legValues = cut.FindAll("#draft-leg option").Select(o => o.GetAttribute("value") ?? string.Empty).ToArray();
        Assert.Equal(new[] { string.Empty }, legValues);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() =>
        {
            var (_, request) = Assert.Single(ingestion.Updated);
            Assert.Equal(tripId, request.TripId);
            Assert.Null(request.TripLegId);
        });
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
    public void OnlyMessagesThatDidNotYieldItemsOfferReprocessing()
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

    [Fact]
    public void RefreshingInboxHistoryRefetchesFromTheApi()
    {
        var parsed = new InboxEmailDto(Guid.NewGuid(), "traveler@contoso.com", "Flight confirmation", DateTimeOffset.UtcNow, ParseStatus.Parsed);
        var client = new StubInboxHistoryApiClient([parsed]);
        Services.AddSingleton<IInboxHistoryApiClient>(client);

        var cut = RenderComponent<InboxHistory>();
        cut.WaitForAssertion(() => Assert.Equal(1, client.Fetches));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Refresh", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, client.Fetches));
    }

    [Fact]
    public void RefreshingPendingDraftsRefetchesFromTheApi()
    {
        var client = new StubEmailIngestionApiClient([Draft(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 12, 9, 30, 0))]);
        Services.AddSingleton<IEmailIngestionApiClient>(client);

        var cut = RenderComponent<InboxDrafts>();
        cut.WaitForAssertion(() => Assert.Equal(1, client.Fetches));

        cut.FindAll("button").Single(b => b.TextContent.Contains("Refresh", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, client.Fetches));
    }

    private sealed class StubEmailIngestionApiClient(IReadOnlyList<ParsedItemDraftDto> drafts) : IEmailIngestionApiClient
    {
        private readonly List<ParsedItemDraftDto> _drafts = [.. drafts];

        public List<Guid> Discarded { get; } = [];

        public List<(Guid DraftId, UpdateParsedItemDraftRequest Request)> Updated { get; } = [];

        /// <summary>Set to make the next save or confirm fail the way the API would.</summary>
        public ApiError? NextError { get; set; }

        public int Fetches { get; private set; }

        public Task<IReadOnlyList<ParsedItemDraftDto>> GetDraftsAsync(CancellationToken ct = default)
        {
            Fetches++;
            return Task.FromResult<IReadOnlyList<ParsedItemDraftDto>>([.. _drafts]);
        }

        public Task<DraftMutationResult<ParsedItemDraftDto>> UpdateDraftAsync(Guid draftId, UpdateParsedItemDraftRequest request, CancellationToken ct = default)
        {
            Updated.Add((draftId, request));
            if (NextError is { } error)
            {
                NextError = null;
                return Task.FromResult(new DraftMutationResult<ParsedItemDraftDto>(null, error));
            }

            var index = _drafts.FindIndex(d => d.ParsedItemDraftId == draftId);
            if (index < 0) return Task.FromResult(new DraftMutationResult<ParsedItemDraftDto>(null, ApiError.NotFoundOrDenied()));

            var updated = _drafts[index] with
            {
                TripId = request.TripId,
                TripLegId = request.TripLegId,
                ItemType = request.ItemType,
                Title = request.Title,
                Location = request.Location,
                StartLocal = request.StartLocal,
                StartTimeZoneId = request.StartTimeZoneId,
                EndLocal = request.EndLocal,
                EndTimeZoneId = request.EndTimeZoneId,
                ConfirmationCode = request.ConfirmationCode,
                Notes = request.Notes
            };
            _drafts[index] = updated;
            return Task.FromResult(new DraftMutationResult<ParsedItemDraftDto>(updated, null));
        }

        public Task<DraftMutationResult<ConfirmParsedItemDraftResponse>> ConfirmDraftAsync(Guid draftId, CancellationToken ct = default)
        {
            if (NextError is { } error)
            {
                NextError = null;
                return Task.FromResult(new DraftMutationResult<ConfirmParsedItemDraftResponse>(null, error));
            }

            _drafts.RemoveAll(d => d.ParsedItemDraftId == draftId);
            return Task.FromResult(new DraftMutationResult<ConfirmParsedItemDraftResponse>(
                new ConfirmParsedItemDraftResponse(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), null));
        }

        public Task<bool> DiscardDraftAsync(Guid draftId, CancellationToken ct = default)
        {
            Discarded.Add(draftId);
            _drafts.RemoveAll(d => d.ParsedItemDraftId == draftId);
            return Task.FromResult(true);
        }
    }

    private sealed class StubInboxHistoryApiClient(IReadOnlyList<InboxEmailDto> items) : IInboxHistoryApiClient
    {
        public List<Guid> Reprocessed { get; } = [];

        public int Fetches { get; private set; }

        public Task<IReadOnlyList<InboxEmailDto>> GetHistoryAsync(CancellationToken ct = default)
        {
            Fetches++;
            return Task.FromResult(items);
        }

        public Task<bool> ReprocessEmailAsync(Guid inboxEmailId, CancellationToken ct = default)
        {
            Reprocessed.Add(inboxEmailId);
            return Task.FromResult(true);
        }
    }
}
