using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Extensions;

namespace TripPlanner.Api.Features.Notifications;

public static class NotificationEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var notifications = endpoints.MapGroup("/api/notifications")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Notifications");

        notifications.MapGetNotificationCount();
        notifications.MapGetNotifications();
        notifications.MapMarkNotificationRead();
        notifications.MapMarkAllNotificationsRead();
        notifications.MapCompleteNotification();
        notifications.MapDeleteNotification();

        var preferences = endpoints.MapGroup("/api/notification-preferences")
            .RequireAuthorization(WebApplicationBuilderExtensions.AuthenticatedUserPolicy)
            .WithTags("Notification preferences");

        preferences.MapGetNotificationPreferences();
        preferences.MapUpdateNotificationPreference();

        return endpoints;
    }
}
