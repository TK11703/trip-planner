using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class DeleteNotificationEndpoint
{
    public static RouteGroupBuilder MapDeleteNotification(this RouteGroupBuilder group)
    {
        group.MapDelete("/{notificationId:guid}", HandleAsync).WithName("DeleteNotification");
        return group;
    }

    private static async Task<Results<NoContent, NotFound<ApiError>>> HandleAsync(
        Guid notificationId,
        ICurrentUser currentUser,
        INotificationRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var deleted = await repository.DeleteAsync(currentUser.UserId, notificationId, clock.UtcNow, cancellationToken);
        if (!deleted)
        {
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        return TypedResults.NoContent();
    }
}
