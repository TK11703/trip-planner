using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class GetNotificationPreferencesEndpoint
{
    public static RouteGroupBuilder MapGetNotificationPreferences(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync).WithName("GetNotificationPreferences");
        return group;
    }

    private static async Task<Ok<NotificationPreferencesResponse>> HandleAsync(
        ICurrentUser currentUser,
        INotificationRepository repository,
        CancellationToken cancellationToken)
    {
        var stored = await repository.GetPreferencesAsync(currentUser.UserId, cancellationToken);

        var categories = NotificationCategories.All.Select(definition =>
        {
            var match = stored.FirstOrDefault(p => string.Equals(p.Category, definition.Category, StringComparison.OrdinalIgnoreCase));
            return new NotificationPreferenceResponse(
                definition.Category,
                definition.DisplayName,
                match?.InAppEnabled ?? definition.DefaultInAppEnabled,
                match?.EmailEnabled ?? definition.DefaultEmailEnabled,
                match?.UpdatedAtUtc);
        }).ToArray();

        return TypedResults.Ok(new NotificationPreferencesResponse(categories));
    }
}
