using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TripPlanner.Api.Security;
using TripPlanner.Contracts.Common;
using TripPlanner.Contracts.Errors;
using TripPlanner.Contracts.Notifications;
using TripPlanner.Database.Notifications;

namespace TripPlanner.Api.Features.Notifications;

public static class CompleteNotificationEndpoint
{
    public static RouteGroupBuilder MapCompleteNotification(this RouteGroupBuilder group)
    {
        group.MapPost("/{notificationId:guid}/complete", HandleAsync).WithName("CompleteNotification");
        return group;
    }

    private static async Task<Results<Ok<CompleteNotificationResponse>, NotFound<ApiError>, Conflict<ApiError>>> HandleAsync(
        Guid notificationId,
        ICurrentUser currentUser,
        INotificationRepository repository,
        NotificationValidator validator,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var existing = await repository.GetAsync(userId, notificationId, cancellationToken);
        if (existing is null)
        {
            return TypedResults.NotFound(ApiError.NotFoundOrDenied());
        }

        var error = validator.ValidateCompletable(existing);
        if (error is not null)
        {
            return TypedResults.Conflict(error);
        }

        var completed = await repository.CompleteAsync(userId, notificationId, userId, currentUser.DisplayName, clock.UtcNow, cancellationToken);
        if (completed is null)
        {
            // Lost a race (already completed or deleted between read and write).
            return TypedResults.Conflict(new ApiError("conflict", "This notification could not be completed."));
        }

        var completedBy = new NotificationCompletedBy(completed.CompletedByUserId!, completed.CompletedByDisplayName);
        return TypedResults.Ok(new CompleteNotificationResponse(
            completed.NotificationId,
            completed.ActionStatus,
            completed.CompletedAtUtc!.Value,
            completedBy));
    }
}
