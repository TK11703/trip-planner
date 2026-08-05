using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class GetDraftListEndpoint
{
    public static RouteGroupBuilder MapGetDraftList(this RouteGroupBuilder group)
    {
        group.MapGet("/drafts", HandleAsync).WithName("GetEmailDraftList");
        return group;
    }

    private static async Task<Ok<ParsedItemDraftListResponse>> HandleAsync(
        ICurrentUser currentUser,
        IParsedItemDraftRepository draftRepository,
        DraftPlacementMatcher placementMatcher,
        CancellationToken cancellationToken)
    {
        var drafts = await draftRepository.GetPendingAsync(currentUser.UserId, cancellationToken);

        // One candidate-leg read serves every draft in the response, so the queue costs the same
        // whether it holds one draft or fifty (SC-008).
        var candidateLegs = await draftRepository.GetPlacementCandidateLegsAsync(
            currentUser.UserId, currentUser.Email, cancellationToken);

        var items = drafts.Select(d => d.ToDto(placementMatcher.Match(d, candidateLegs))).ToArray();
        return TypedResults.Ok(new ParsedItemDraftListResponse(items));
    }
}
