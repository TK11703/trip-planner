using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Components.Trips;
using TripPlanner.Web.Features.Trips;
using TripPlanner.Web.Tests.Infrastructure;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

public class TripsIndexMotivationTests : TestContext
{
    private static TripSummary Trip(string name) =>
        new(Guid.NewGuid(), name, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 6), DateTimeOffset.UtcNow, 0, TripAccessLevel.Owner, true);

    // US1 (FR-001/FR-002): enhanced travel-oriented header copy.
    [Fact]
    public void TripsIndex_RendersEnhancedTravelOrientedHeader()
    {
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient());

        var cut = RenderComponent<TripsIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("organized", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("trips you own", cut.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("shared with you", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    // US2 (FR-003/FR-006/SC-002): zero-trip state shows facts and keeps the create action.
    [Fact]
    public void EmptyState_ShowsMotivationalFactsWithCreateAction()
    {
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient());

        var cut = RenderComponent<TripsIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Travel planning tips", cut.Markup);
            Assert.Contains("Create your first trip", cut.Markup);
            foreach (var fact in MotivationalTravelFacts.All.Take(3))
            {
                Assert.Contains(fact.Title, cut.Markup);
            }
        });
    }

    // US2 (FR-007): facts appear as secondary content in a sparse list.
    [Fact]
    public void SparseState_ShowsFactsAsSecondaryContent()
    {
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(new[] { Trip("Weekend away"), Trip("City break") }));

        var cut = RenderComponent<TripsIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Weekend away", cut.Markup);
            Assert.Contains("Travel planning tips", cut.Markup);
            Assert.NotNull(cut.Find("section.motivational-facts.motivational-facts-secondary"));
        });
    }

    // US2 (FR-007): a dense list hides the supporting facts.
    [Fact]
    public void DenseState_HidesMotivationalFacts()
    {
        var many = Enumerable.Range(0, 6).Select(i => Trip($"Trip {i}")).ToArray();
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient(many));

        var cut = RenderComponent<TripsIndex>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Trip 0", cut.Markup);
            Assert.DoesNotContain("Travel planning tips", cut.Markup);
        });
    }

    // US2 (FR-005/SC-003): curated facts are short and there are at least three.
    [Fact]
    public void CuratedFacts_AreShortAndSufficientlyNumerous()
    {
        Assert.True(MotivationalTravelFacts.All.Count >= 3);
        Assert.All(MotivationalTravelFacts.All, fact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fact.Title));
            Assert.False(string.IsNullOrWhiteSpace(fact.Body));
            Assert.True(fact.Body.Length < 140, $"Fact '{fact.Title}' body should be under 140 characters.");
        });
    }

    // US3 (FR-010): facts introduce no interactive controls or keyboard focus stops.
    [Fact]
    public void MotivationalFacts_AreNonInteractive()
    {
        Services.AddSingleton<ITripApiClient>(new StubTripApiClient());

        var cut = RenderComponent<TripsIndex>();

        cut.WaitForAssertion(() =>
        {
            var section = cut.Find("section.motivational-facts");
            Assert.Empty(section.QuerySelectorAll("a, button, input, select, textarea, [tabindex]"));
        });
    }
}
