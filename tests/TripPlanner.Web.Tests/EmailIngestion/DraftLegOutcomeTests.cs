using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages.EmailIngestion;
using TripPlanner.Web.Features.EmailIngestion;
using TripPlanner.Web.Features.Timezones;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Web.Tests.EmailIngestion;

/// <summary>
/// The traveler decides what a draft becomes, and the decision is recoverable in both directions
/// without losing work (FR-011, FR-022 to FR-027). Also covers the one labelled suggestion the
/// review screen is allowed to offer (FR-034).
/// </summary>
public class DraftLegOutcomeTests : TestContext
{
    private static readonly Guid TripId = Guid.NewGuid();

    private static readonly DateTime DefaultEnd = new(2026, 8, 12, 17, 45, 0);

    private static ParsedItemDraftDto LegDraft(
        string? endZone = "America/New_York",
        string? origin = "SEA",
        string? destination = "JFK",
        string? mode = TransportationModes.Flight,
        bool omitEnd = false,
        decimal? travelCost = 412.50m,
        string? currency = "USD") =>
        new(Guid.NewGuid(), Guid.NewGuid(), TripId, null, "flight", "Flight ABC123", "SEA",
            new DateTime(2026, 8, 12, 9, 30, 0), "America/Los_Angeles",
            omitEnd ? null : DefaultEnd, endZone,
            "ABC123", null, 0.92,
            ReviewStatus.PendingReview, DateTimeOffset.UtcNow, null,
            DraftOutcome.Leg, origin, destination, mode, travelCost, currency, null,
            DraftRecognitionState.Current);

    private static ParsedItemDraftDto ItemDraft() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TripId, null, "hotel", "Hotel Stay", "Seattle",
            new DateTime(2026, 8, 12, 15, 0, 0), "America/Los_Angeles", null, null,
            "HTL1", null, 0.92,
            ReviewStatus.PendingReview, DateTimeOffset.UtcNow, null,
            DraftOutcome.Item, null, null, null, null, null, null,
            DraftRecognitionState.Current);

    private static (TripSummary Summary, TripDetail Detail) EditableTrip()
    {
        var summary = new TripSummary(TripId, "West Coast",
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20),
            DateTimeOffset.UtcNow, 0, TripAccessLevel.Owner);
        var detail = new TripDetail(TripId, "West Coast", null,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);
        return (summary, detail);
    }

    private (IRenderedComponent<InboxDrafts> Cut, StubEmailIngestionApiClient Ingestion) RenderQueue(ParsedItemDraftDto draft)
    {
        var ingestion = new StubEmailIngestionApiClient([draft]);
        var (summary, detail) = EditableTrip();
        var tripClient = new StubTripApiClient([summary]);
        tripClient.Details[TripId] = detail;

        Services.AddSingleton<IEmailIngestionApiClient>(ingestion);
        Services.AddSingleton<ITripApiClient>(tripClient);
        Services.AddSingleton<ITimezoneOptionsProvider>(new TimezoneOptionsProvider());

        var cut = RenderComponent<InboxDrafts>();
        cut.WaitForAssertion(() => Assert.Contains(draft.Title!, cut.Markup, StringComparison.Ordinal));
        return (cut, ingestion);
    }

    private static void OpenEditModal(IRenderedComponent<InboxDrafts> cut)
    {
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Edit").Click();
        cut.WaitForAssertion(() => Assert.Contains("Edit parsed item", cut.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void ALegDraftRendersTheRouteFieldsAndNotTheItemFields()
    {
        var (cut, _) = RenderQueue(LegDraft());
        OpenEditModal(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("#draft-mode"));
            Assert.NotNull(cut.Find("#draft-origin"));
            Assert.NotNull(cut.Find("#draft-destination"));
            Assert.NotNull(cut.Find("#draft-travel-cost"));
            Assert.NotNull(cut.Find("#draft-end-tz"));

            // An item's type and its containing leg are meaningless for a leg.
            Assert.Empty(cut.FindAll("#draft-type"));
            Assert.Empty(cut.FindAll("#draft-leg"));
        });
    }

    [Fact]
    public void AnItemDraftRendersTheItemFieldsAndNotTheRouteFields()
    {
        var (cut, _) = RenderQueue(ItemDraft());
        OpenEditModal(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("#draft-type"));
            Assert.NotNull(cut.Find("#draft-leg"));
            Assert.Empty(cut.FindAll("#draft-mode"));
            Assert.Empty(cut.FindAll("#draft-origin"));
            Assert.Empty(cut.FindAll("#draft-destination"));
        });
    }

    /// <summary>The recognized currency labels the amount without being editable (research D9).</summary>
    [Fact]
    public void TheTravelCostShowsTheRecognizedCurrencyAsALabel()
    {
        var (cut, _) = RenderQueue(LegDraft());
        OpenEditModal(cut);

        cut.WaitForAssertion(() =>
            Assert.Equal("USD", cut.Find("[data-testid=draft-travel-cost-currency]").TextContent.Trim()));
    }

    [Fact]
    public void SwitchingToItemNamesWhatWillNotCarryAcross()
    {
        var (cut, _) = RenderQueue(LegDraft());
        OpenEditModal(cut);

        cut.Find("#draft-outcome-item").Change(true);

        cut.WaitForAssertion(() =>
        {
            var notice = cut.Find("[data-testid=draft-carry-notice]").TextContent;
            Assert.Contains("Origin", notice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("destination", notice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("travel mode", notice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cost", notice, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void SwitchingToLegNamesWhatWillNotCarryAcross()
    {
        var (cut, _) = RenderQueue(ItemDraft());
        OpenEditModal(cut);

        cut.Find("#draft-outcome-leg").Change(true);

        cut.WaitForAssertion(() =>
        {
            var notice = cut.Find("[data-testid=draft-carry-notice]").TextContent;
            Assert.Contains("item type", notice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("leg isn't placed inside another leg", notice, StringComparison.OrdinalIgnoreCase);
        });
    }

    /// <summary>
    /// The heart of FR-025 and SC-004: switching away and back must lose nothing. The notice says
    /// values "won't appear on" the other outcome precisely because they are still there.
    /// </summary>
    [Fact]
    public void SwitchingToItemAndBackLeavesTheRouteIntact()
    {
        var (cut, _) = RenderQueue(LegDraft());
        OpenEditModal(cut);

        cut.Find("#draft-outcome-item").Change(true);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#draft-origin")));

        cut.Find("#draft-outcome-leg").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("SEA", cut.Find("#draft-origin").GetAttribute("value"));
            Assert.Equal("JFK", cut.Find("#draft-destination").GetAttribute("value"));
            Assert.Equal(TransportationModes.Flight, cut.Find("#draft-mode").GetAttribute("value"));
            Assert.Equal("412.50", cut.Find("#draft-travel-cost").GetAttribute("value"));
        });
    }

    [Fact]
    public void SavingALegDraftSendsTheRouteAndOutcome()
    {
        var (cut, ingestion) = RenderQueue(LegDraft());
        OpenEditModal(cut);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();

        cut.WaitForAssertion(() =>
        {
            var (_, request) = Assert.Single(ingestion.Updated);
            Assert.Equal(DraftOutcome.Leg, request.ProposedOutcome);
            Assert.Equal("SEA", request.Origin);
            Assert.Equal("JFK", request.Destination);
            Assert.Equal(TransportationModes.Flight, request.TransportationMode);
            Assert.Equal(412.50m, request.TravelCost);
        });
    }

    /// <summary>
    /// Every gap is named on the same pass, so the traveler sees the whole picture at once
    /// instead of being sent round the loop once per missing field (FR-031).
    /// </summary>
    [Fact]
    public void EveryMissingLegDetailIsNamedAtOnce()
    {
        var (cut, ingestion) = RenderQueue(LegDraft(endZone: null, origin: null, destination: null, mode: null));
        OpenEditModal(cut);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();

        cut.WaitForAssertion(() =>
        {
            var markup = cut.Markup;
            Assert.Contains("End timezone is required for a trip leg.", markup, StringComparison.Ordinal);
            Assert.Contains("Choose how you are traveling", markup, StringComparison.Ordinal);
            Assert.Contains("Enter where this travel leg starts from.", markup, StringComparison.Ordinal);
            Assert.Contains("Enter where this travel leg arrives.", markup, StringComparison.Ordinal);
        });

        // Nothing reached the API while the draft was incomplete (FR-032).
        Assert.Empty(ingestion.Updated);
    }

    /// <summary>
    /// Regression guard. The queue writes the chosen placement immediately before confirming, and
    /// that request replaces the whole stored row. Built without the transport fields it silently
    /// reset the draft to an item, so a forwarded flight became a reservation no matter what the
    /// traveler had chosen — and every unit test passed, because the fake repository was updated
    /// through the modal rather than this path (FR-024, FR-025).
    /// </summary>
    [Fact]
    public void ConfirmingALegDraftFromTheQueueNeverRewritesItToAnItem()
    {
        var draft = LegDraft() with
        {
            Placement = new DraftPlacement(
                DraftPlacementStatus.Matched, TripId, Guid.NewGuid(),
                [new PlacementCandidate(TripId, "West Coast", Guid.NewGuid(), "Seattle stay",
                    new DateTimeOffset(2026, 8, 11, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 8, 16, 0, 0, 0, TimeSpan.Zero))])
        };

        var (cut, ingestion) = RenderQueue(draft);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Confirm").Click();

        cut.WaitForAssertion(() =>
        {
            // Whatever else the queue writes on the way to confirming, it must never demote the
            // outcome or drop the route.
            foreach (var (_, request) in ingestion.Updated)
            {
                Assert.Equal(DraftOutcome.Leg, request.ProposedOutcome);
                Assert.Equal("SEA", request.Origin);
                Assert.Equal("JFK", request.Destination);
                Assert.Equal(TransportationModes.Flight, request.TransportationMode);
            }
        });
    }

    // ---- The labelled one-click suggestion (FR-034, research D5) ----

    /// <summary>
    /// The suggestion is shown but not applied. The field stays empty and the missing-detail
    /// message stays up until the traveler presses the button.
    /// </summary>
    [Fact]
    public void AMissingEndZoneOffersASuggestionWithoutFillingTheField()
    {
        var (cut, _) = RenderQueue(LegDraft(endZone: null));
        OpenEditModal(cut);

        cut.WaitForAssertion(() =>
        {
            var row = cut.Find("[data-testid=draft-suggestion-endTimeZoneId]");
            Assert.Contains("didn't state an arrival time zone", row.TextContent, StringComparison.Ordinal);
            Assert.Contains("America/Los_Angeles", row.TextContent, StringComparison.Ordinal);

            // Shown, not applied: the control still holds nothing.
            Assert.True(string.IsNullOrEmpty(cut.Find("#draft-end-tz").GetAttribute("value")));
        });
    }

    [Fact]
    public void PressingUseThisFillsTheFieldAndRetiresTheSuggestion()
    {
        var (cut, ingestion) = RenderQueue(LegDraft(endZone: null));
        OpenEditModal(cut);

        cut.WaitForAssertion(() => cut.Find("[data-testid=draft-suggestion-accept-endTimeZoneId]"));
        cut.Find("[data-testid=draft-suggestion-accept-endTimeZoneId]").Click();

        // Once accepted it is the traveler's own value, so the offer is not made again.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=draft-suggestion-endTimeZoneId]")));

        // The accepted value is what actually gets saved — the point of the whole exercise.
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();
        cut.WaitForAssertion(() =>
        {
            var (_, request) = Assert.Single(ingestion.Updated);
            Assert.Equal("America/Los_Angeles", request.EndTimeZoneId);
        });
    }

    [Fact]
    public void AnEndZoneAlreadyPresentIsNeverSecondGuessed()
    {
        var (cut, ingestion) = RenderQueue(LegDraft(endZone: "America/New_York"));
        OpenEditModal(cut);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=draft-suggestion-endTimeZoneId]")));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();
        cut.WaitForAssertion(() =>
        {
            var (_, request) = Assert.Single(ingestion.Updated);
            Assert.Equal("America/New_York", request.EndTimeZoneId);
        });
    }

    /// <summary>
    /// No default is offered for a missing end date/time. Every candidate would either be
    /// invented or produce a zero-length leg, so the traveler is asked instead (research D5).
    /// </summary>
    [Fact]
    public void NoSuggestionIsOfferedForAMissingEndDateTime()
    {
        var (cut, ingestion) = RenderQueue(LegDraft(omitEnd: true, endZone: null));
        OpenEditModal(cut);

        // The end zone may be suggested; the end date/time never is.
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=draft-suggestion-endLocal]")));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Save").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("End date and time is required for a trip leg.", cut.Markup, StringComparison.Ordinal));
        Assert.Empty(ingestion.Updated);
    }

    [Fact]
    public void AnItemDraftIsOfferedNoEndZoneSuggestion()
    {
        var (cut, _) = RenderQueue(ItemDraft());
        OpenEditModal(cut);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=draft-suggestion-endTimeZoneId]")));
    }

    // ---- Re-recognition of drafts that predate this feature (FR-045) ----

    /// <summary>
    /// A legacy draft is re-read once, when it is opened. Never from the list: the queue is read
    /// on every visit, and a provider call there would be charged to every traveler every time.
    /// </summary>
    [Fact]
    public void OpeningAPendingDraftAsksForReRecognitionExactlyOnce()
    {
        var draft = ItemDraft() with { TransportRecognitionState = DraftRecognitionState.Pending };
        var (cut, ingestion) = RenderQueue(draft);

        // Merely listing must not trigger it.
        Assert.Equal(0, ingestion.ReRecognizeCalls);

        ingestion.ReRecognizeResult = LegDraft() with
        {
            ParsedItemDraftId = draft.ParsedItemDraftId,
            TransportRecognitionState = DraftRecognitionState.Current
        };

        OpenEditModal(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, ingestion.ReRecognizeCalls);
            // The refreshed draft is what the modal binds, so the route it recovered is shown.
            Assert.Equal("SEA", cut.Find("#draft-origin").GetAttribute("value"));
        });
    }

    [Theory]
    [InlineData(DraftRecognitionState.Current)]
    [InlineData(DraftRecognitionState.Unavailable)]
    public void ADraftThatIsAlreadySettledIsNotReRecognized(DraftRecognitionState state)
    {
        var (cut, ingestion) = RenderQueue(LegDraft() with { TransportRecognitionState = state });

        OpenEditModal(cut);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#draft-origin")));
        Assert.Equal(0, ingestion.ReRecognizeCalls);
    }

    /// <summary>
    /// Recognition being unavailable is not a failure of the traveler's action: the draft still
    /// opens, still holds its values, and is still confirmable on the item path (FR-047).
    /// </summary>
    [Fact]
    public void AFailedReRecognitionStillOpensTheDraft()
    {
        var draft = ItemDraft() with { TransportRecognitionState = DraftRecognitionState.Pending };
        var (cut, ingestion) = RenderQueue(draft);
        ingestion.ReRecognizeResult = null;

        OpenEditModal(cut);

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, ingestion.ReRecognizeCalls);
            Assert.Equal("Hotel Stay", cut.Find("#draft-title").GetAttribute("value"));
            Assert.NotNull(cut.Find("#draft-type"));
        });
    }

    /// <summary>
    /// A re-recognition that throws rather than returning null must be equally harmless. Letting
    /// it escape `OnInitializedAsync` tears down the Blazor circuit and leaves an empty form on
    /// screen — strictly worse than an un-enriched draft, and how this surfaced in practice.
    /// </summary>
    [Fact]
    public void AThrowingReRecognitionStillOpensTheDraftIntact()
    {
        var draft = ItemDraft() with { TransportRecognitionState = DraftRecognitionState.Pending };
        var (cut, ingestion) = RenderQueue(draft);
        ingestion.ReRecognizeThrows = new InvalidOperationException("provider exploded");

        OpenEditModal(cut);

        cut.WaitForAssertion(() =>
        {
            // The draft opened, bound to the values it already had.
            Assert.Equal("Hotel Stay", cut.Find("#draft-title").GetAttribute("value"));
            Assert.Equal("Seattle", cut.Find("#draft-location").GetAttribute("value"));
        });
    }
}
