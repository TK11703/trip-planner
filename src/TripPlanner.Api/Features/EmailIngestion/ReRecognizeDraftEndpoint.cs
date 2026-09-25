using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Brings a draft recognized before this feature up to date (FR-045).
///
/// Its own endpoint rather than a step inside <c>GET /drafts</c>: the review queue is read on
/// every visit, and putting a provider call there would charge every traveler for every draft
/// every time. The review screen calls this once, for the one draft being opened.
///
/// There is deliberately no failure status. Recognition being unavailable is not a failure of
/// the traveler's action — they asked to open a draft, and the draft opens either way (FR-047).
/// </summary>
public static class ReRecognizeDraftEndpoint
{
    public static RouteGroupBuilder MapReRecognizeDraft(this RouteGroupBuilder group)
    {
        group.MapPost("/drafts/{id:guid}/re-recognize", HandleAsync).WithName("ReRecognizeEmailDraft");
        return group;
    }

    private static async Task<Results<Ok<ParsedItemDraftDto>, NotFound>> HandleAsync(
        Guid id,
        ICurrentUser currentUser,
        DraftReRecognitionService reRecognition,
        IParsedItemDraftRepository draftRepository,
        DraftPlacementMatcher placementMatcher,
        CancellationToken cancellationToken)
    {
        var draft = await reRecognition.ReRecognizeAsync(id, currentUser.UserId, cancellationToken);
        if (draft is null) return TypedResults.NotFound();

        // The re-read can move the dates, so the placement is recomputed against them, exactly as
        // an edit does.
        var candidateLegs = await draftRepository.GetPlacementCandidateLegsAsync(
            currentUser.UserId, currentUser.Email, cancellationToken);

        return TypedResults.Ok(draft.ToDto(placementMatcher.Match(draft, candidateLegs)));
    }
}
