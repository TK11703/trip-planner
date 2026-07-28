using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.EmailIngestion;
using TripPlanner.Database.EmailIngestion;

namespace TripPlanner.Api.Features.EmailIngestion;

/// <summary>
/// Development-only endpoint that lets developers inject a raw email directly into the
/// ingestion pipeline without requiring live Azure Communication Services or Event Grid.
/// Restricted to the Development environment at registration time.
/// </summary>
public static class DevInjectEmailEndpoint
{
    public static RouteGroupBuilder MapDevInjectEmail(this RouteGroupBuilder group)
    {
        group.MapPost("/dev-inject", HandleAsync).WithName("DevInjectEmail");
        return group;
    }

    private static async Task<Ok> HandleAsync(
        DevInjectEmailRequest request,
        ICurrentUser currentUser,
        IInboxEmailRepository emailRepository,
        EmailDeduplicationService dedup,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var receivedAt = DateTimeOffset.UtcNow;
        var hash = dedup.ComputeHash(request.Sender, request.Subject, receivedAt);

        await emailRepository.InsertAsync(
            new NewInboxEmail(userId, request.Sender, request.Subject, request.BodyText, request.BodyHtml, receivedAt, hash),
            cancellationToken);

        return TypedResults.Ok();
    }
}
