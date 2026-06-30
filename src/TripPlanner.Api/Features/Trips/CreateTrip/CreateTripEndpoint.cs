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

namespace TripPlanner.Api.Features.Trips.CreateTrip;

public static class CreateTripEndpoint
{
    public static RouteGroupBuilder MapCreateTrip(this RouteGroupBuilder group)
    {
        group.MapPost("/", HandleAsync).WithName("CreateTrip");
        return group;
    }

    private static async Task<Results<Created<CreateTripResponse>, BadRequest<ApiError>>> HandleAsync(
        CreateTripRequest request,
        ICurrentUser currentUser,
        CreateTripValidator validator,
        ITripCommandRepository commands,
        IAuditRepository audit,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(ownerId, AuditOperations.TripCreate, "trip", null, AuditResults.ValidationFailed, clock.UtcNow, cancellationToken);
            return TypedResults.BadRequest(validation.Error!);
        }
        var tripId = await commands.InsertAsync(ownerId, request, clock.UtcNow, cancellationToken);
        await audit.RecordAsync(ownerId, AuditOperations.TripCreate, "trip", tripId.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        var response = new CreateTripResponse(tripId, request.Name, request.Destination, request.Description, request.StartDate, request.EndDate);
        return TypedResults.Created($"/api/trips/{tripId}", response);
    }
}
