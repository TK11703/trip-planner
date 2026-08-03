using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Re-runs recognition against a message the traveler already owns. Processing happens inside
/// this request and returns the same conclusive outcome shape as relay ingestion — there is no
/// queue to re-enter.
/// </summary>
public static class ReprocessEmailEndpoint
{
    public static RouteGroupBuilder MapReprocessEmail(this RouteGroupBuilder group)
    {
        group.MapPost("/inbox/{id:guid}/reprocess", HandleAsync)
            .WithName("ReprocessInboxEmail")
            .Produces<IngestRelayMessageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces<IngestRelayMessageResponse>(StatusCodes.Status502BadGateway);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        ICurrentUser currentUser,
        IInboxEmailRepository emailRepository,
        RelayMessageProcessor processor,
        CancellationToken cancellationToken)
    {
        var email = await emailRepository.GetByIdAsync(id, currentUser.UserId, cancellationToken);
        if (email is null)
        {
            return Results.NotFound();
        }

        var result = await processor.ReprocessAsync(email, cancellationToken);
        return Results.Json(result.Body, statusCode: result.StatusCode);
    }
}
