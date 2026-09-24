using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Features.Notifications;
using TripPlanner.Api.Features.TripItems;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Audit;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.Audit;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Turns a reviewed draft into a timeline item. Feature 024, FR-019 to FR-025: this path runs the
/// same validator, records the same audit entry, and raises the same notification the item form
/// does, so no email-created item exists that the traveler could not have typed by hand.
/// </summary>
public static class ConfirmDraftEndpoint
{
    public static RouteGroupBuilder MapConfirmDraft(this RouteGroupBuilder group)
    {
        group.MapPost("/drafts/{id:guid}/confirm", HandleAsync).WithName("ConfirmEmailDraft");
        return group;
    }

    private static async Task<Results<Ok<ConfirmParsedItemDraftResponse>, NotFound<ApiError>, BadRequest<ApiError>>> HandleAsync(
        Guid id,
        ICurrentUser currentUser,
        IParsedItemDraftRepository draftRepository,
        ITripAccessResolver accessResolver,
        TrackedItemValidator validator,
        ITripReadRepository tripReads,
        ITripItemRepository items,
        IAuditRepository audit,
        IItineraryNotificationService itineraryNotifications,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var callerId = currentUser.UserId;
        var draft = await draftRepository.GetByIdAsync(id, callerId, cancellationToken);
        if (draft is null) return TypedResults.NotFound(ApiError.NotFoundOrDenied());

        // A leg is optional: an item with no leg lands in the timeline's unassigned area, where the
        // traveler can relate it to a leg later. A trip is still required — an item has to belong
        // somewhere (FR-011).
        if (draft.TripId is not { } tripId)
            return TypedResults.BadRequest(ApiError.ValidationFailed("Choose a trip before confirming this item.", "tripId"));

        // Nothing is invented on the traveler's behalf. A detail the parser could not read is
        // named back so they can supply it, rather than being filled with a placeholder (FR-023).
        if (draft.StartLocal is not { } startLocal)
            return TypedResults.BadRequest(ApiError.ValidationFailed("Start date and time is required.", "startLocal"));

        if (string.IsNullOrWhiteSpace(draft.StartTimeZoneId))
            return TypedResults.BadRequest(ApiError.ValidationFailed("Start timezone is required.", "startTimeZoneId"));

        if (string.IsNullOrWhiteSpace(draft.Title))
            return TypedResults.BadRequest(ApiError.ValidationFailed("Title is required.", "title"));

        var access = await accessResolver.ResolveAsync(callerId, tripId, cancellationToken);
        if (access is null || !access.CanEditContent())
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "tracked-item", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var ownerId = access.OwnerUserId;
        var trip = await tripReads.GetDetailAsync(ownerId, tripId, cancellationToken);
        if (trip is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "tracked-item", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        trip = trip with { Legs = await items.GetLegsAsync(ownerId, tripId, cancellationToken) };

        var request = new CreateTrackedItemRequest(
            TripLegId: draft.TripLegId,
            ItemType: NormalizeItemType(draft.ItemType),
            Title: draft.Title,
            Location: draft.Location,
            StartLocal: startLocal,
            StartTimeZoneId: draft.StartTimeZoneId,
            EndLocal: draft.EndLocal,
            EndTimeZoneId: draft.EndTimeZoneId,
            DisplayColor: TrackedItemColors.Default,
            ConfirmationCode: draft.ConfirmationCode,
            Notes: draft.Notes,
            EstimatedCost: null);

        // The same validator the item form runs, so an out-of-window leg is refused here exactly
        // as it is there — and nothing is written when it is (FR-019, FR-020, FR-022).
        var validation = validator.Validate(request, trip);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(callerId, AuditOperations.TrackedItemCreate, "tracked-item", tripId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, cancellationToken);
            return TypedResults.BadRequest(validation.Error!);
        }

        var createdId = await items.CreateTrackedItemAsync(ownerId, tripId, request, clock.UtcNow, cancellationToken);
        if (createdId is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "tracked-item", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        // The draft points at the item it became, keeping the path from a forwarded email to an
        // itinerary entry traceable (FR-025).
        await draftRepository.SetReviewStatusAsync(id, callerId, "confirmed", createdId.Value, cancellationToken);
        await audit.RecordAsync(callerId, AuditOperations.TrackedItemCreate, "tracked-item", createdId.Value.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        await itineraryNotifications.NotifyChangeAsync(tripId, ownerId, callerId, currentUser.DisplayName, ItineraryChangeKind.TripItemCreated, createdId.Value, cancellationToken);

        return TypedResults.Ok(new ConfirmParsedItemDraftResponse(createdId.Value, tripId, draft.TripLegId));
    }

    /// <summary>
    /// A draft holds either a recognizer type ("flight", "hotel", "car_rental", "other") or one of
    /// the tracked types the traveler picked in the draft editor. A tracked type is honoured as
    /// chosen; a recognizer type maps onto the closest one.
    /// </summary>
    private static string NormalizeItemType(string? raw)
    {
        var value = raw?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value)) return TrackedItemTypes.Event;
        if (TrackedItemTypes.All.Contains(value)) return value;

        return value switch
        {
            "flight" or "hotel" or "car_rental" => TrackedItemTypes.Reservation,
            _ => TrackedItemTypes.Event
        };
    }
}
