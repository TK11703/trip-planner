using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.Trips.GetTrips;

public static class GetTripsEndpoint
{
    public static RouteGroupBuilder MapGetTrips(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync).WithName("GetTrips");
        return group;
    }

    private static async Task<Ok<TripListResponse>> HandleAsync(
        int? page,
        int? pageSize,
        ICurrentUser currentUser,
        ITripReadRepository repository,
        IAuditRepository audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        var effectivePage = QueryLimits.CoerceTripPage(page);
        var effectivePageSize = QueryLimits.CoerceTripPageSize(pageSize);
        var trips = await repository.GetPageAsync(ownerId, currentUser.Email, effectivePage, effectivePageSize, cancellationToken);
        await audit.RecordAsync(ownerId, AuditOperations.TripRead, "trip-list", null, AuditResults.Success, clock.UtcNow, cancellationToken);
        return TypedResults.Ok(trips);
    }
}
