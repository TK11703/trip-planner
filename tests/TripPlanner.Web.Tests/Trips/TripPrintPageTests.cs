using Bunit;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Contracts.Places;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Components.Pages.Trips;
using TripPlanner.Web.Features.Trips;
using Xunit;

namespace TripPlanner.Web.Tests.Trips;

// User Story 1 (P1): the printable page renders a chrome-free trip document, offers a
// Print action, and shows empty / denied states.
public class TripPrintPageTests : TestContext
{
    private IRenderedComponent<TripPrint> Render(TripDetail? trip)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ITripApiClient>(new PrintStubTripApiClient(trip));
        return RenderComponent<TripPrint>(p => p.Add(x => x.TripId, Guid.NewGuid()));
    }

    [Fact]
    public void PopulatedTrip_RendersDocumentAndPrintAction()
    {
        var cut = Render(TripFixtures.Populated());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Japan 2026", cut.Markup);
            Assert.Single(cut.FindAll("article.tp-print-doc"));
            // A Print button exists inside the print-only-hidden action bar.
            var printButton = cut.FindAll(".tp-print-actions button").First();
            Assert.Equal("Print", printButton.TextContent.Trim());
            // Back link returns to the trip.
            Assert.Contains(cut.FindAll(".tp-print-actions a"), a => a.TextContent.Contains("Back"));
        });
    }

    [Fact]
    public void RendersNoAppChrome()
    {
        var cut = Render(TripFixtures.Populated());

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("nav.navbar"));
            Assert.Empty(cut.FindAll("footer"));
        });
    }

    [Fact]
    public void EmptyTrip_ShowsCoherentPage()
    {
        var cut = Render(TripFixtures.Empty());

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Empty trip", cut.Markup);
            Assert.Contains("no legs or events", cut.Markup);
        });
    }

    [Fact]
    public void DeniedOrMissingTrip_ShowsNotAvailableState()
    {
        var cut = Render(null);

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("article.tp-print-doc"));
            Assert.Contains("isn't available", cut.Markup);
        });
    }

    private sealed class PrintStubTripApiClient : ITripApiClient
    {
        private readonly TripDetail? _trip;
        public PrintStubTripApiClient(TripDetail? trip) => _trip = trip;

        public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(_trip);

        public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
