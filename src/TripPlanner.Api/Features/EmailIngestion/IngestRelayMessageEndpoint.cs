using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Contracts.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Accepts one email relayed by the external automation (Logic App) that monitors the trip
/// mailbox. The message is processed to completion inside this request and the response always
/// carries a conclusive outcome, so the relay never has to poll for a result.
///
/// The API does not monitor any mailbox itself. Mailbox access, retry, and dead-lettering are
/// the relay's responsibility.
/// </summary>
public static class IngestRelayMessageEndpoint
{
    public static RouteGroupBuilder MapIngestRelayMessage(this RouteGroupBuilder group)
    {
        group.MapPost("/messages", HandleAsync)
            .WithName("IngestRelayMessage")
            .WithSummary("Ingest a relayed email message.")
            .Produces<IngestRelayMessageResponse>(StatusCodes.Status200OK)
            .Produces<IngestRelayMessageResponse>(StatusCodes.Status400BadRequest)
            .Produces<IngestRelayMessageResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<IngestRelayMessageResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<IngestRelayMessageResponse>(StatusCodes.Status502BadGateway);
        return group;
    }

    private static async Task<IResult> HandleAsync(
        IngestRelayMessageRequest? request,
        RelayMessageProcessor processor,
        CancellationToken cancellationToken)
    {
        var result = await processor.IngestAsync(request, cancellationToken);
        return Results.Json(result.Body, statusCode: result.StatusCode);
    }
}
