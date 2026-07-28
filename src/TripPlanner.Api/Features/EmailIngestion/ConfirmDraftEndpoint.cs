using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.TripItems;
using TripPlanner.Database.EmailIngestion;
using TripPlanner.Database.TripItems;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class ConfirmDraftEndpoint
{
    public static RouteGroupBuilder MapConfirmDraft(this RouteGroupBuilder group)
    {
        group.MapPost("/drafts/{id:guid}/confirm", HandleAsync).WithName("ConfirmEmailDraft");
        return group;
    }

    private static async Task<Results<Ok<ConfirmParsedEventDraftResponse>, NotFound, BadRequest<string>>> HandleAsync(
        Guid id,
        ICurrentUser currentUser,
        IParsedEventDraftRepository draftRepository,
        ITripItemRepository tripItemRepository,
        CancellationToken cancellationToken)
    {
        var draft = await draftRepository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (draft is null) return TypedResults.NotFound();

        if (draft.TripId is null || draft.TripLegId is null)
            return TypedResults.BadRequest("Draft must have a TripId and TripLegId assigned before confirming.");

        if (draft.StartLocal is null)
            return TypedResults.BadRequest("Draft must have a StartLocal date/time before confirming.");

        var request = new CreateTrackedItemRequest(
            TripLegId: draft.TripLegId.Value,
            ItemType: NormalizeEventType(draft.EventType),
            Title: draft.Title ?? draft.EventType ?? "Imported event",
            Location: draft.Location,
            StartLocal: draft.StartLocal.Value,
            StartTimeZoneId: draft.StartTimeZoneId ?? "UTC",
            EndLocal: draft.EndLocal,
            EndTimeZoneId: draft.EndTimeZoneId,
            DisplayColor: TrackedItemColors.Default,
            ConfirmationCode: draft.ConfirmationCode,
            Notes: draft.Notes,
            EstimatedCost: null);

        var createdId = await tripItemRepository.CreateTrackedItemAsync(
            currentUser.UserId, draft.TripId.Value, request, DateTimeOffset.UtcNow, cancellationToken);

        if (createdId is null)
            return TypedResults.NotFound();

        await draftRepository.SetReviewStatusAsync(id, currentUser.UserId, "confirmed", cancellationToken);

        return TypedResults.Ok(new ConfirmParsedEventDraftResponse(createdId.Value, draft.TripId.Value, draft.TripLegId.Value));
    }

    private static string NormalizeEventType(string? raw)
    {
        return raw?.ToLowerInvariant() switch
        {
            "flight" or "hotel" or "car_rental" or "activity" => TrackedItemTypes.Reservation,
            _ => TrackedItemTypes.Event
        };
    }
}
