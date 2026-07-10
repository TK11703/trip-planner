using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Features.Trips;

namespace TripPlanner.Web.Tests.Infrastructure;

/// <summary>
/// Minimal ITripApiClient stub for component tests. Returns an optional recent-trip
/// list and safe defaults for everything else.
/// </summary>
public sealed class StubTripApiClient : ITripApiClient
{
    private readonly IReadOnlyList<TripSummary> _recent;

    public StubTripApiClient(IReadOnlyList<TripSummary>? recent = null) => _recent = recent ?? Array.Empty<TripSummary>();

    public Task<TripListResponse> GetTripsAsync(int page = 1, int pageSize = 12, CancellationToken ct = default) => Task.FromResult(new TripListResponse(_recent, page, pageSize, _recent.Count));
    public Task<IReadOnlyList<TripSummary>> GetRecentAsync(int? limit = null, CancellationToken ct = default) => Task.FromResult(_recent);
    public Task<TripDetail?> GetDetailAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripDetail?>(null);
    public Task<CreateTripResponse> CreateAsync(CreateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<CreateTripResponse> UpdateAsync(Guid tripId, UpdateTripRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteTripAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task CreateLegAsync(Guid tripId, CreateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateLegAsync(Guid tripId, Guid tripLegId, UpdateTripLegRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteLegAsync(Guid tripId, Guid tripLegId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripLegDefaultsResponse?> GetLegDefaultsAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripLegDefaultsResponse?>(null);
    public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<TripTimelineResponse?>(null);
    public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripShareMember>>(Array.Empty<TripShareMember>());
    public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DirectoryUserResult>>(Array.Empty<DirectoryUserResult>());
    public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>>(Array.Empty<TripPlanner.Contracts.Places.PlaceSuggestion>());
}
