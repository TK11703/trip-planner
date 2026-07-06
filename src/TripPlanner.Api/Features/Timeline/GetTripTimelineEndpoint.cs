using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Timeline;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Timeline;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.Timeline;

public static class GetTripTimelineEndpoint
{
    private const int SlotMinutes = 30;

    public static RouteGroupBuilder MapGetTripTimeline(this RouteGroupBuilder group)
    {
        group.MapGet("/timeline", HandleAsync).WithName("GetTripTimeline");
        return group;
    }

    private static async Task<Results<Ok<TripTimelineResponse>, NotFound<ApiError>>> HandleAsync(
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
        var projection = await timeline.GetTimelineAsync(ownerId, tripId, ct);
        var (startDate, endDate) = ComputeRange(trip, projection);
        await audit.RecordAsync(ownerId, AuditOperations.TimelineRead, "timeline", tripId.ToString(), AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.Ok(new TripTimelineResponse(tripId, startDate, endDate, SlotMinutes, projection.Legs, projection.UnassignedItems));
    }

    private static (DateOnly Start, DateOnly End) ComputeRange(TripDetail trip, TimelineProjection projection)
    {
        var start = trip.StartDate;
        var end = trip.EndDate;

        void Expand(DateOnly date)
        {
            if (date < start) start = date;
            if (date > end) end = date;
        }

        foreach (var leg in projection.Legs)
        {
            Expand(DateOnly.FromDateTime(leg.StartLocal));
            Expand(DateOnly.FromDateTime(leg.EndLocal));

            var tz = TimezoneOptions.FindTimeZone(leg.StartTimeZoneId) ?? TimeZoneInfo.Utc;
            foreach (var item in leg.Items)
            {
                Expand(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(item.StartsAt, tz).DateTime));
                if (item.EndsAt is { } itemEnd)
                {
                    Expand(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(itemEnd, tz).DateTime));
                }
            }
        }

        foreach (var item in projection.UnassignedItems)
        {
            Expand(DateOnly.FromDateTime(item.StartsAt.UtcDateTime));
            if (item.EndsAt is { } itemEnd)
            {
                Expand(DateOnly.FromDateTime(itemEnd.UtcDateTime));
            }
        }

        return (start, end);
    }
}
