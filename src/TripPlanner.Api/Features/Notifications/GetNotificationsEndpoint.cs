using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class GetNotificationsEndpoint
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    public static RouteGroupBuilder MapGetNotifications(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync).WithName("GetNotifications");
        return group;
    }

    private static async Task<Ok<NotificationListResponse>> HandleAsync(
        int? limit,
        ICurrentUser currentUser,
        INotificationRepository repository,
        ITripAccessResolver accessResolver,
        CancellationToken cancellationToken)
    {
        var recipientId = currentUser.UserId;
        var coercedLimit = limit is null || limit <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
        var records = await repository.GetListAsync(recipientId, coercedLimit, cancellationToken);

        var items = new List<NotificationResponse>(records.Count);
        foreach (var record in records)
        {
            var canOpenTrip = false;
            if (record.TargetType == NotificationTargetType.Trip && record.RelatedTripId is Guid tripId)
            {
                var access = await accessResolver.ResolveAsync(recipientId, tripId, cancellationToken);
                canOpenTrip = access is not null;
            }

            items.Add(record.ToResponse(canOpenTrip));
        }

        return TypedResults.Ok(new NotificationListResponse(items));
    }
}
