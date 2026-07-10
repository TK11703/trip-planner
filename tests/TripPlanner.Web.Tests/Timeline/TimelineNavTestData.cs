using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Contracts.Trips;
using TripPlanner.Web.Features.Trips;

namespace TripPlanner.Web.Tests.Timeline;

/// <summary>
/// Shared helpers for the 014 timeline date-navigation component tests: a builder for a
/// multi-day timeline response and a minimal <see cref="ITripApiClient"/> stub.
/// </summary>
internal static class TimelineNavTestData
{
    public static TripTimelineResponse Response(DateOnly start, DateOnly end)
    {
        var legStart = start.ToDateTime(new TimeOnly(8, 0));
        var legEnd = end.ToDateTime(new TimeOnly(18, 0));
        var leg = new TimelineLeg(
            Guid.NewGuid(),
            "Main leg",
            null,
            null,
            legStart,
            "UTC",
            "UTC",
            legEnd,
            "UTC",
            "UTC",
            0,
            Array.Empty<TimelineItem>());

        return new TripTimelineResponse(
            Guid.NewGuid(),
            start,
            end,
            30,
            new[] { leg },
            Array.Empty<TimelineItem>());
    }
}

internal sealed class NavStubTripApiClient : ITripApiClient
{
    private readonly TripTimelineResponse _timeline;
    public NavStubTripApiClient(TripTimelineResponse timeline) => _timeline = timeline;

    public Task<TripTimelineResponse?> GetTimelineAsync(Guid tripId, CancellationToken ct = default)
        => Task.FromResult<TripTimelineResponse?>(_timeline);

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
    public Task CreateItemAsync(Guid tripId, CreateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task UpdateItemAsync(Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task DeleteItemAsync(Guid tripId, Guid trackedItemId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripShareMember>> GetSharesAsync(Guid tripId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<DirectoryUserResult>> SearchDirectoryUsersAsync(Guid tripId, string query, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripShareMember> UpsertShareAsync(Guid tripId, UpsertTripShareRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<TripShareMember> UpdateShareAccessAsync(Guid tripId, string userId, UpdateTripShareAccessRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RemoveShareAsync(Guid tripId, string userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>> SuggestPlacesAsync(string query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TripPlanner.Contracts.Places.PlaceSuggestion>>(Array.Empty<TripPlanner.Contracts.Places.PlaceSuggestion>());
    public Task<TripMapResponse> GetTripMapAsync(Guid tripId, CancellationToken ct = default) => Task.FromResult(new TripMapResponse(Array.Empty<TripMapLocation>()));
}
