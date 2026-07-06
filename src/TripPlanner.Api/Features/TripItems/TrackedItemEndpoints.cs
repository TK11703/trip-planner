using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.TripItems;

public static class TrackedItemEndpoints
{
    public static RouteGroupBuilder MapTrackedItems(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateAsync).WithName("CreateTrackedItem");
        group.MapPut("/{trackedItemId:guid}", UpdateAsync).WithName("UpdateTrackedItem");
        group.MapDelete("/{trackedItemId:guid}", DeleteAsync).WithName("DeleteTrackedItem");
        return group;
    }

    private static async Task<Results<Created, BadRequest<ApiError>, NotFound<ApiError>>> CreateAsync(
        Guid tripId, CreateTrackedItemRequest request,
        ICurrentUser currentUser, TrackedItemValidator validator,
        ITripReadRepository tripReads, ITripItemRepository items,
        IAuditRepository audit, IClock clock, CancellationToken ct)
    {
        var ownerId = currentUser.UserId;
        var trip = await tripReads.GetDetailAsync(ownerId, tripId, ct);
        if (trip is null)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "tracked-item", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        trip = trip with { Legs = await items.GetLegsAsync(ownerId, tripId, ct) };
        var validation = validator.Validate(request, trip);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(ownerId, AuditOperations.TrackedItemCreate, "tracked-item", tripId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(validation.Error!);
        }
        var id = await items.CreateTrackedItemAsync(ownerId, tripId, request, clock.UtcNow, ct);
        if (id is null)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "tracked-item", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        await audit.RecordAsync(ownerId, AuditOperations.TrackedItemCreate, "tracked-item", id.Value.ToString(), AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.Created($"/api/trips/{tripId}/items/{id}");
    }

    private static async Task<Results<NoContent, BadRequest<ApiError>, NotFound<ApiError>>> UpdateAsync(
        Guid tripId, Guid trackedItemId, UpdateTrackedItemRequest request,
        ICurrentUser currentUser, TrackedItemValidator validator,
        ITripReadRepository tripReads, ITripItemRepository items,
        IAuditRepository audit, IClock clock, CancellationToken ct)
    {
        var ownerId = currentUser.UserId;
        var trip = await tripReads.GetDetailAsync(ownerId, tripId, ct);
        if (trip is null)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "tracked-item", trackedItemId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        trip = trip with { Legs = await items.GetLegsAsync(ownerId, tripId, ct) };
        var validation = validator.Validate(request, trip);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(ownerId, AuditOperations.TrackedItemUpdate, "tracked-item", trackedItemId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(validation.Error!);
        }
        var affected = await items.UpdateTrackedItemAsync(ownerId, tripId, trackedItemId, request, ct);
        if (affected == 0)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "tracked-item", trackedItemId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        await audit.RecordAsync(ownerId, AuditOperations.TrackedItemUpdate, "tracked-item", trackedItemId.ToString(), AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, NotFound<ApiError>>> DeleteAsync(
        Guid tripId, Guid trackedItemId,
        ICurrentUser currentUser, ITripItemRepository items,
        IAuditRepository audit, IClock clock, CancellationToken ct)
    {
        var ownerId = currentUser.UserId;
        var affected = await items.DeleteTrackedItemAsync(ownerId, tripId, trackedItemId, ct);
        if (affected == 0)
        {
            await audit.RecordAsync(ownerId, AuditOperations.AccessDenied, "tracked-item", trackedItemId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        await audit.RecordAsync(ownerId, AuditOperations.TrackedItemDelete, "tracked-item", trackedItemId.ToString(), AuditResults.Success, clock.UtcNow, ct);
        return TypedResults.NoContent();
    }
}
