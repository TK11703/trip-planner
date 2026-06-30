using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.Trips.UpdateTrip;

public static class UpdateTripEndpoint
{
    public static RouteGroupBuilder MapUpdateTrip(this RouteGroupBuilder group)
    {
        group.MapPut("/{tripId:guid}", HandleAsync).WithName("UpdateTrip");
        return group;
    }

    private static async Task<Results<Ok<CreateTripResponse>, BadRequest<ApiError>, NotFound<ApiError>>> HandleAsync(
        Guid tripId,
        UpdateTripRequest request,
        ICurrentUser currentUser,
        UpdateTripValidator validator,
        ITripCommandRepository commands,
        IAuditRepository audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(ownerId, AuditOperations.TripUpdate, "trip", tripId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, cancellationToken);
            return TypedResults.BadRequest(validation.Error!);
        }
        var affected = await commands.UpdateAsync(ownerId, tripId, request, cancellationToken);
        if (affected == 0)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "trip", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        await audit.RecordAsync(ownerId, AuditOperations.TripUpdate, "trip", tripId.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        return TypedResults.Ok(new CreateTripResponse(tripId, request.Name, request.Destination, request.Description, request.StartDate, request.EndDate));
    }
}
