using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class MarkAllNotificationsReadEndpoint
{
    public static RouteGroupBuilder MapMarkAllNotificationsRead(this RouteGroupBuilder group)
    {
        group.MapPost("/read-all", HandleAsync).WithName("MarkAllNotificationsRead");
        return group;
    }

    private static async Task<Ok<MarkAllNotificationsReadResponse>> HandleAsync(
        ICurrentUser currentUser,
        INotificationRepository repository,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var nowUtc = clock.UtcNow;
        var count = await repository.MarkAllReadAsync(currentUser.UserId, nowUtc, cancellationToken);
        return TypedResults.Ok(new MarkAllNotificationsReadResponse(count, nowUtc));
    }
}
