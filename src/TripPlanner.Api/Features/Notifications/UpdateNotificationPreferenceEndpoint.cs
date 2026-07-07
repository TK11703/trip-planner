using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class UpdateNotificationPreferenceEndpoint
{
    public static RouteGroupBuilder MapUpdateNotificationPreference(this RouteGroupBuilder group)
    {
        group.MapPut("/{category}", HandleAsync).WithName("UpdateNotificationPreference");
        return group;
    }

    private static async Task<Results<Ok<NotificationPreferenceResponse>, NotFound<ApiError>, UnprocessableEntity<ApiError>>> HandleAsync(
        string category,
        UpdateNotificationPreferenceRequest request,
        ICurrentUser currentUser,
        INotificationRepository repository,
        NotificationPreferenceValidator validator,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!validator.IsKnownCategory(category))
        {
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        if (!validator.IsValid(request))
        {
            return TypedResults.UnprocessableEntity(ApiError.ValidationFailed("Notification preference is invalid."));
        }

        var definition = NotificationCategories.Resolve(category);
        var record = await repository.UpsertPreferenceAsync(currentUser.UserId, definition.Category, request.InAppEnabled, request.EmailEnabled, clock.UtcNow, cancellationToken);

        return TypedResults.Ok(new NotificationPreferenceResponse(
            definition.Category,
            definition.DisplayName,
            record.InAppEnabled,
            record.EmailEnabled,
            record.UpdatedAtUtc));
    }
}
