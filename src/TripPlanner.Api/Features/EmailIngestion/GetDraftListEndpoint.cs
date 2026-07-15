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

    private static async Task<Ok<ParsedEventDraftListResponse>> HandleAsync(
        ICurrentUser currentUser,
        IParsedEventDraftRepository draftRepository,
        CancellationToken cancellationToken)
    {
        var drafts = await draftRepository.GetPendingAsync(currentUser.UserId, cancellationToken);
        var items = drafts.Select(d => d.ToDto()).ToArray();
        return TypedResults.Ok(new ParsedEventDraftListResponse(items));
    }
}
