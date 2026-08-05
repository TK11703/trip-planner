using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class DiscardDraftEndpoint
{
    public static RouteGroupBuilder MapDiscardDraft(this RouteGroupBuilder group)
    {
        group.MapPost("/drafts/{id:guid}/discard", HandleAsync).WithName("DiscardEmailDraft");
        return group;
    }

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid id,
        ICurrentUser currentUser,
        IParsedItemDraftRepository draftRepository,
        CancellationToken cancellationToken)
    {
        var success = await draftRepository.SetReviewStatusAsync(id, currentUser.UserId, "discarded", ct: cancellationToken);
        return success ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
