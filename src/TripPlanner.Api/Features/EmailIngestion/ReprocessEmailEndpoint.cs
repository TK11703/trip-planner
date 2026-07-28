using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

public static class ReprocessEmailEndpoint
{
    public static RouteGroupBuilder MapReprocessEmail(this RouteGroupBuilder group)
    {
        group.MapPost("/inbox/{id:guid}/reprocess", HandleAsync).WithName("ReprocessInboxEmail");
        return group;
    }

    private static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid id,
        ICurrentUser currentUser,
        IInboxEmailRepository emailRepository,
        CancellationToken cancellationToken)
    {
        // Reset to pending so the background service picks it up again.
        await emailRepository.UpdateParseStatusAsync(id, currentUser.UserId, "pending", cancellationToken);
        return TypedResults.NoContent();
    }
}
