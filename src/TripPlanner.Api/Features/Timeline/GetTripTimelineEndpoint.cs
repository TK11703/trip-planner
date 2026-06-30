using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Timeline;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.Timeline;

public static class GetTripTimelineEndpoint
{
    public static RouteGroupBuilder MapGetTripTimeline(this RouteGroupBuilder group)
    {
        group.MapGet("/timeline", HandleAsync).WithName("GetTripTimeline");
        return group;
    }

    private static async Task<Results<Ok<TimelineResponse>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        ICurrentUser currentUser,
        ITripReadRepository tripReads,
        ITimelineRepository timeline,
        IAuditRepository audit,
        IClock clock,
        CancellationToken ct)
    {
        var ownerId = currentUser.UserId;
        var trip = await tripReads.GetDetailAsync(ownerId, tripId, ct);
        if (trip is null)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "timeline", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        var events = await timeline.GetTimelineAsync(ownerId, tripId, ct);
        await audit.RecordAsync(ownerId, AuditOperations.TimelineRead, "timeline", tripId.ToString(), AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.Ok(new TimelineResponse(tripId, trip.StartDate, trip.EndDate, events));
    }
}
