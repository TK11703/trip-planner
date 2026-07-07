using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class GetNotificationCountEndpoint
{
    public static RouteGroupBuilder MapGetNotificationCount(this RouteGroupBuilder group)
    {
        group.MapGet("/count", HandleAsync).WithName("GetNotificationCount");
        return group;
    }

    private static async Task<Ok<NotificationCountResponse>> HandleAsync(
        ICurrentUser currentUser,
        INotificationRepository repository,
        CancellationToken cancellationToken)
    {
        var count = await repository.GetUnreadCountAsync(currentUser.UserId, cancellationToken);
        return TypedResults.Ok(new NotificationCountResponse(count));
    }
}
