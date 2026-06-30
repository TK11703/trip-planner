using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.Trips.GetRecentTrips;

public static class GetRecentTripsEndpoint
{
    public static RouteGroupBuilder MapGetRecentTrips(this RouteGroupBuilder group)
    {
        group.MapGet("/recent", HandleAsync).WithName("GetRecentTrips");
        return group;
    }

    private static async Task<Ok<IReadOnlyList<TripSummary>>> HandleAsync(
        int? limit,
        ICurrentUser currentUser,
        ITripReadRepository repository,
        IAuditRepository audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId; // authorization required at endpoint level
        var effectiveLimit = QueryLimits.CoerceRecentTripLimit(limit);
        var trips = await repository.GetRecentAsync(ownerId, effectiveLimit, cancellationToken);
        await audit.RecordAsync(ownerId, AuditOperations.TripRead, "trip-list", null, AuditResults.Success, clock.UtcNow, cancellationToken);
        return TypedResults.Ok(trips);
    }
}
