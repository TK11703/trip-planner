using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.Audit;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Trips;
using TripPlanner.Database.TripSharing;

namespace TripPlanner.Api.Features.TripItems;

public static class TripLegEndpoints
{
    public static RouteGroupBuilder MapTripLegs(this RouteGroupBuilder group)
    {
        group.MapGet("/defaults", GetDefaultsAsync).WithName("GetTripLegDefaults");
        group.MapPost("/", CreateAsync).WithName("CreateTripLeg");
        group.MapPut("/{tripLegId:guid}", UpdateAsync).WithName("UpdateTripLeg");
        group.MapDelete("/{tripLegId:guid}", DeleteAsync).WithName("DeleteTripLeg");
        return group;
    }

    private static async Task<Results<Ok<TripLegDefaultsResponse>, NotFound<ApiError>>> GetDefaultsAsync(
        Guid tripId,
        ICurrentUser currentUser,
        ITripAccessResolver accessResolver,
        ITripItemRepository items,
        IAuditRepository audit,
        IClock clock,
        CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var defaults = await items.GetLegDefaultsAsync(access.OwnerUserId, tripId, ct);
        if (defaults is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        return TypedResults.Ok(defaults);
    }

    private static async Task<Results<Created, BadRequest<ApiError>, NotFound<ApiError>>> CreateAsync(
        Guid tripId, CreateTripLegRequest request,
        ICurrentUser currentUser, ITripAccessResolver accessResolver, TripLegValidator validator,
        ITripReadRepository tripReads, ITripItemRepository items,
        IAuditRepository audit, IItineraryNotificationService itineraryNotifications, IClock clock, CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null || !access.CanEditContent())
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        var ownerId = access.OwnerUserId;
        var trip = await tripReads.GetDetailAsync(ownerId, tripId, ct);
        if (trip is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        var validation = validator.Validate(request, trip);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(callerId, AuditOperations.TripLegCreate, "trip-leg", tripId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(validation.Error!);
        }
        var id = await items.CreateLegAsync(ownerId, tripId, request, clock.UtcNow, ct);
        if (id is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        await audit.RecordAsync(callerId, AuditOperations.TripLegCreate, "trip-leg", id.Value.ToString(), AuditResults.Success, clock.UtcNow, ct);
        await itineraryNotifications.NotifyChangeAsync(tripId, ownerId, callerId, currentUser.DisplayName, ItineraryChangeKind.TripLegCreated, ct);
        return TypedResults.Created($"/api/trips/{tripId}/legs/{id}");
    }

    private static async Task<Results<NoContent, BadRequest<ApiError>, NotFound<ApiError>>> UpdateAsync(
        Guid tripId, Guid tripLegId, UpdateTripLegRequest request,
        ICurrentUser currentUser, ITripAccessResolver accessResolver, TripLegValidator validator,
        ITripReadRepository tripReads, ITripItemRepository items,
        IAuditRepository audit, IItineraryNotificationService itineraryNotifications, IClock clock, CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null || !access.CanEditContent())
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripLegId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        var ownerId = access.OwnerUserId;
        var trip = await tripReads.GetDetailAsync(ownerId, tripId, ct);
        if (trip is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripLegId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        var validation = validator.Validate(request, trip);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(callerId, AuditOperations.TripLegUpdate, "trip-leg", tripLegId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(validation.Error!);
        }
        var affected = await items.UpdateLegAsync(ownerId, tripId, tripLegId, request, ct);
        if (affected == 0)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripLegId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        await audit.RecordAsync(callerId, AuditOperations.TripLegUpdate, "trip-leg", tripLegId.ToString(), AuditResults.Success, clock.UtcNow, ct);
        await itineraryNotifications.NotifyChangeAsync(tripId, ownerId, callerId, currentUser.DisplayName, ItineraryChangeKind.TripLegUpdated, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, BadRequest<ApiError>, NotFound<ApiError>>> DeleteAsync(
        Guid tripId, Guid tripLegId,
        ICurrentUser currentUser, ITripAccessResolver accessResolver, ITripItemRepository items,
        IAuditRepository audit, IItineraryNotificationService itineraryNotifications, IClock clock, CancellationToken ct)
    {
        var callerId = currentUser.UserId;
        var access = await accessResolver.ResolveAsync(callerId, tripId, ct);
        if (access is null || !access.CanEditContent())
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripLegId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        var ownerId = access.OwnerUserId;
        var relatedItems = await items.CountItemsForLegAsync(ownerId, tripId, tripLegId, ct);
        if (relatedItems > 0)
        {
            await audit.RecordAsync(callerId, AuditOperations.TripLegDelete, "trip-leg", tripLegId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, ct);
            return TypedResults.BadRequest(ApiError.ValidationFailed(
                "This trip leg still has related events. Reassign or remove those events before deleting the leg.",
                "tripLegId"));
        }
        var affected = await items.DeleteLegAsync(ownerId, tripId, tripLegId, ct);
        if (affected == 0)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripLegId.ToString(), AuditResults.Denied, clock.UtcNow, ct);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }
        await audit.RecordAsync(callerId, AuditOperations.TripLegDelete, "trip-leg", tripLegId.ToString(), AuditResults.Success, clock.UtcNow, ct);
        await itineraryNotifications.NotifyChangeAsync(tripId, ownerId, callerId, currentUser.DisplayName, ItineraryChangeKind.TripLegDeleted, ct);
        return TypedResults.NoContent();
    }
}
