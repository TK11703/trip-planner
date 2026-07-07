using TripPlanner.Contracts.Notifications;

namespace TripPlanner.Api.Features.Notifications;

/// <summary>Validates notification preference updates and category identifiers.</summary>
public sealed class NotificationPreferenceValidator
{
    public bool IsKnownCategory(string category)
        => NotificationCategories.All.Any(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));

    public bool IsValid(UpdateNotificationPreferenceRequest? request)
        => request is not null;
}
