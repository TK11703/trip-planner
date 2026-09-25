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
using TripPlanner.Contracts.Trips;
using TripPlanner.Database.Audit;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.TripItems;
using TripPlanner.Database.Trips;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Turns a reviewed draft into a timeline entry. Feature 024, FR-019 to FR-025: this path runs
/// the same validator, records the same audit entry, and raises the same notification the entry
/// form does, so no email-created entry exists that the traveler could not have typed by hand.
///
/// Feature 028 adds the leg branch. A draft proposing a leg goes through
/// <see cref="TripLegValidator"/> and the leg creation path, exactly as the leg form does; a
/// draft proposing an item follows the original path untouched. The two branches are mutually
/// exclusive, so a confirmed draft records an item id or a leg id but never both (FR-014).
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
        TripLegValidator legValidator,
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

        var outcome = EmailIngestionMapping.ParseOutcome(draft.ProposedOutcome);

        // A leg is optional for an item: one with no leg lands in the timeline's unassigned area,
        // where the traveler can relate it to a leg later. A trip is still required either way —
        // an item has to belong somewhere, and a leg has to belong to a trip (FR-011).
        if (draft.TripId is not { } tripId)
            return TypedResults.BadRequest(ApiError.ValidationFailed(
                outcome == DraftOutcome.Leg
                    ? "Choose a trip before confirming this trip leg."
                    : "Choose a trip before confirming this item.",
                "tripId"));

        // Nothing is invented on the traveler's behalf. A detail the parser could not read is
        // named back so they can supply it, rather than being filled with a placeholder (FR-023,
        // FR-028). A leg demands more than an item does, so the two outcomes ask different
        // questions and nothing is written until whichever set is satisfied.
        if (outcome == DraftOutcome.Leg)
        {
            if (LegConfirmationGaps.Find(draft) is { } gap)
                return TypedResults.BadRequest(gap);
        }
        else
        {
            if (draft.StartLocal is null)
                return TypedResults.BadRequest(ApiError.ValidationFailed("Start date and time is required.", "startLocal"));

            if (string.IsNullOrWhiteSpace(draft.StartTimeZoneId))
                return TypedResults.BadRequest(ApiError.ValidationFailed("Start timezone is required.", "startTimeZoneId"));

            if (string.IsNullOrWhiteSpace(draft.Title))
                return TypedResults.BadRequest(ApiError.ValidationFailed("Title is required.", "title"));
        }

        var auditResource = outcome == DraftOutcome.Leg ? "trip-leg" : "tracked-item";

        var access = await accessResolver.ResolveAsync(callerId, tripId, cancellationToken);
        if (access is null || !access.CanEditContent())
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, auditResource, tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var ownerId = access.OwnerUserId;
        var trip = await tripReads.GetDetailAsync(ownerId, tripId, cancellationToken);
        if (trip is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, auditResource, tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        trip = trip with { Legs = await items.GetLegsAsync(ownerId, tripId, cancellationToken) };

        if (outcome == DraftOutcome.Leg)
        {
            return await ConfirmAsLegAsync(
                id, draft, trip, tripId, callerId, ownerId, currentUser.DisplayName,
                draftRepository, legValidator, items, audit, itineraryNotifications, clock, cancellationToken);
        }

        // The three ! below are the item-branch guards above: title, start, and start zone were
        // all checked and refused before this point. They read as suppressions only because the
        // checks now sit in a branch, which flow analysis does not carry across.
        var request = new CreateTrackedItemRequest(
            TripLegId: draft.TripLegId,
            ItemType: NormalizeItemType(draft.ItemType),
            Title: draft.Title!,
            Location: draft.Location,
            StartLocal: draft.StartLocal!.Value,
            StartTimeZoneId: draft.StartTimeZoneId!,
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
        await draftRepository.SetReviewStatusAsync(id, callerId, "confirmed", createdId.Value,
            DraftOutcome.Item.ToPersisted(), null, cancellationToken);
        await audit.RecordAsync(callerId, AuditOperations.TrackedItemCreate, "tracked-item", createdId.Value.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        await itineraryNotifications.NotifyChangeAsync(tripId, ownerId, callerId, currentUser.DisplayName, ItineraryChangeKind.TripItemCreated, createdId.Value, cancellationToken);

        return TypedResults.Ok(new ConfirmParsedItemDraftResponse(
            DraftOutcome.Item, tripId, createdId.Value, draft.TripLegId, null));
    }

    /// <summary>
    /// Creates a trip leg from a draft that has already cleared <see cref="LegConfirmationGaps"/>.
    ///
    /// Deliberately mirrors the leg form's create path step for step — the same
    /// <see cref="TripLegValidator"/>, the same audit operation, and the same
    /// <see cref="ItineraryChangeKind.TripLegCreated"/> notification — so a leg that arrived by
    /// email is indistinguishable from one the traveler typed, and collaborators hear about it
    /// the same way (FR-013, FR-019, FR-039).
    ///
    /// No tracked item is created on this path, and the draft's <c>trip_leg_id</c> is left alone:
    /// where an item would have been placed is a different question from what was created here.
    /// </summary>
    private static async Task<Results<Ok<ConfirmParsedItemDraftResponse>, NotFound<ApiError>, BadRequest<ApiError>>> ConfirmAsLegAsync(
        Guid id,
        ParsedItemDraftRecord draft,
        TripDetail trip,
        Guid tripId,
        string callerId,
        string ownerId,
        string? displayName,
        IParsedItemDraftRepository draftRepository,
        TripLegValidator legValidator,
        ITripItemRepository items,
        IAuditRepository audit,
        IItineraryNotificationService itineraryNotifications,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var legRequest = LegConfirmationGaps.ToLegRequest(draft);

        var validation = legValidator.Validate(legRequest, trip);
        if (!validation.IsValid)
        {
            await audit.RecordAsync(callerId, AuditOperations.TripLegCreate, "trip-leg", tripId.ToString(), AuditResults.ValidationFailed, clock.UtcNow, cancellationToken);
            return TypedResults.BadRequest(validation.Error!);
        }

        var createdLegId = await items.CreateLegAsync(ownerId, tripId, legRequest, clock.UtcNow, cancellationToken);
        if (createdLegId is null)
        {
            await audit.RecordAsync(callerId, AuditOperations.AccessDenied, "trip-leg", tripId.ToString(), AuditResults.Denied, clock.UtcNow, cancellationToken);
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        await draftRepository.SetReviewStatusAsync(id, callerId, "confirmed", null,
            DraftOutcome.Leg.ToPersisted(), createdLegId.Value, cancellationToken);
        await audit.RecordAsync(callerId, AuditOperations.TripLegCreate, "trip-leg", createdLegId.Value.ToString(), AuditResults.Success, clock.UtcNow, cancellationToken);
        await itineraryNotifications.NotifyChangeAsync(tripId, ownerId, callerId, displayName, ItineraryChangeKind.TripLegCreated, createdLegId.Value, cancellationToken);

        return TypedResults.Ok(new ConfirmParsedItemDraftResponse(
            DraftOutcome.Leg, tripId, null, null, createdLegId.Value));
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
