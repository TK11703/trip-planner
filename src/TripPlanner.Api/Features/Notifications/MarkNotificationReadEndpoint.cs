using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class MarkNotificationReadEndpoint
{
    public static RouteGroupBuilder MapMarkNotificationRead(this RouteGroupBuilder group)
    {
        group.MapPost("/{notificationId:guid}/read", HandleAsync).WithName("MarkNotificationRead");
        return group;
    }

    private static async Task<Results<Ok<MarkNotificationReadResponse>, NotFound<ApiError>>> HandleAsync(
        Guid notificationId,
        ICurrentUser currentUser,
        INotificationRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var readAt = await repository.MarkReadAsync(currentUser.UserId, notificationId, clock.UtcNow, cancellationToken);
        if (readAt is null)
        {
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        return TypedResults.Ok(new MarkNotificationReadResponse(notificationId, readAt.Value));
    }
}
