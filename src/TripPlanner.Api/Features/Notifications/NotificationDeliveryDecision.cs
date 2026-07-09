using TripPlanner.Contracts.Notifications;

namespace TripPlanner.Api.Features.Notifications;

/// <summary>The outcome of evaluating a recipient's preferences for one category before delivery.</summary>
public enum NotificationDeliveryOutcome
{
    /// <summary>At least one channel is allowed; the notification should be delivered.</summary>
    Deliver = 0,

    /// <summary>Every channel is disabled for this category; nothing is delivered.</summary>
    SuppressAll = 1
}

/// <summary>
/// The resolved delivery decision for a single recipient and category. Channels reflect the
/// recipient's saved preferences (or category defaults), and are applied before any delivery.
/// </summary>
public sealed record NotificationDeliveryDecision(
    bool InAppAllowed,
    bool EmailAllowed)
{
    public NotificationDeliveryOutcome Outcome
        => InAppAllowed || EmailAllowed ? NotificationDeliveryOutcome.Deliver : NotificationDeliveryOutcome.SuppressAll;

    public static NotificationDeliveryDecision FromPreference(bool inAppEnabled, bool emailEnabled)
        => new(inAppEnabled, emailEnabled);

    public static NotificationDeliveryDecision FromDefaults(string category)
    {
        var definition = NotificationCategories.Resolve(category);
        return new NotificationDeliveryDecision(definition.DefaultInAppEnabled, definition.DefaultEmailEnabled);
    }
}
