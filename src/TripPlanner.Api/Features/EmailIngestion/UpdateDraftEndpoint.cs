using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Contracts.Errors;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class UpdateDraftEndpoint
{
    public static RouteGroupBuilder MapUpdateDraft(this RouteGroupBuilder group)
    {
        group.MapPut("/drafts/{id:guid}", HandleAsync).WithName("UpdateEmailDraft");
        return group;
    }

    private static async Task<Results<Ok<ParsedItemDraftDto>, NotFound, BadRequest<ApiError>>> HandleAsync(
        Guid id,
        UpdateParsedItemDraftRequest request,
        ICurrentUser currentUser,
        IParsedItemDraftRepository draftRepository,
        DraftPlacementMatcher placementMatcher,
        CancellationToken cancellationToken)
    {
        // Ownership first: a draft the caller does not own is simply absent, and no validation
        // detail about it should leak.
        if (await draftRepository.GetByIdAsync(id, currentUser.UserId, cancellationToken) is null)
            return TypedResults.NotFound();

        // The candidate legs double as the authority on which legs the caller may point a draft
        // at, so one read covers both the check below and the placement returned afterwards.
        var candidateLegs = await draftRepository.GetPlacementCandidateLegsAsync(
            currentUser.UserId, currentUser.Email, cancellationToken);

        if (request.TripLegId is { } legId)
        {
            // A leg without a trip, or a leg belonging to a different trip, would put the item
            // somewhere the traveler did not choose (FR-018).
            if (request.TripId is not { } tripId)
                return TypedResults.BadRequest(ApiError.ValidationFailed("Choose a trip before choosing one of its legs.", "tripLegId"));

            var belongs = candidateLegs.Any(leg => leg.TripLegId == legId && leg.TripId == tripId);
            if (!belongs)
                return TypedResults.BadRequest(ApiError.ValidationFailed("That leg does not belong to the selected trip.", "tripLegId"));
        }

        var update = new DraftUpdate(
            request.TripId, request.TripLegId, request.ItemType, request.Title, request.Location,
            request.StartLocal, request.StartTimeZoneId, request.EndLocal, request.EndTimeZoneId,
            request.ConfirmationCode, request.Notes);

        var record = await draftRepository.UpdateAsync(id, currentUser.UserId, update, cancellationToken);
        if (record is null) return TypedResults.NotFound();

        // The edit may have moved the dates, so the suggestion is recomputed against them.
        return TypedResults.Ok(record.ToDto(placementMatcher.Match(record, candidateLegs)));
    }
}
