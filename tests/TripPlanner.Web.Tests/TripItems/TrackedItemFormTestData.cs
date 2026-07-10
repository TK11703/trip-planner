using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Features.Maps;
using TripPlanner.Web.Features.Trips;

namespace TripPlanner.Web.Tests.TripItems;

/// <summary>
/// Shared helpers for the 018 event-form tests: DTO builders and a minimal
/// <see cref="ITripApiClient"/> stub (the form only calls the API on submit).
/// </summary>
internal static class TrackedItemFormTestData
{
    public static TripLegDto Leg(DateTime start, DateTime end)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Main leg",
            null,
            null,
            start,
            "UTC",
            "UTC",
            end,
            "UTC",
            "UTC",
            null,
            0);

    public static TrackedItemDto Item(Guid tripLegId, DateTime start, DateTime end, string itemType = "activity", string? location = null)
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            tripLegId,
            itemType,
            "Existing event",
            location,
            start,
            "UTC",
            new DateTimeOffset(start, TimeSpan.Zero),
            end,
            "UTC",
            new DateTimeOffset(end, TimeSpan.Zero),
            "blue",
            null,
            null,
            0);
}

internal sealed class FormStubTripApiClient : ITripApiClient
{
    public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => Task.CompletedTask;
    public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>>(Array.Empty<TripPlanner.Contracts.Places.PlaceSuggestion>());
    public Task<TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(new TripMapResponse(Array.Empty<TripMapLocation>()));
}

/// <summary>Test map-preference provider returning a fixed provider (defaults to Bing).</summary>
internal sealed class StubMapPreferenceProvider : IMapPreferenceProvider
{
    private readonly string _provider;
    public StubMapPreferenceProvider(string provider = "Bing") => _provider = provider;
    public ValueTask<string> GetProviderAsync(CancellationToken ct = default) => ValueTask.FromResult(_provider);
    public void Invalidate() { }
}
