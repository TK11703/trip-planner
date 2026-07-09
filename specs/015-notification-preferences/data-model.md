# Data Model: Notification Preferences

## User Profile

Represents the signed-in person's profile and consolidated preference surface.

**Fields**

- `userId`: Stable user identity.
- `firstName`, `lastName`, `displayName`, `email`, `timeZoneId`: Existing profile fields.
- `notificationPreferences`: Profile-owned notification preference group.
- `updatedAtUtc`: Last profile update timestamp.
- `notificationUpdatedAtUtc`: Last notification preference update timestamp.

**Relationships**

- Owns zero or more `Notification Preference` values by category.
- Supplies recipient email and preference state for `Delivery Decision` evaluation.

**Validation Rules**

- Only the signed-in person can view or change their own profile notification preferences.
- Preference updates must not alter another user's profile or notification delivery.
- Profile responses must show default preferences when a category has no explicit saved preference.

## Notification Preference

Represents a person's saved delivery choice for a notification category.

**Fields**

- `userId`: Person who owns the preference.
- `category`: Notification category key (`ItineraryChanges`, `TripSharing`).
- `displayName`: User-facing category name.
- `inAppEnabled`: Whether future in-app delivery is allowed.
- `emailEnabled`: Whether future email delivery is allowed.
- `updatedAtUtc`: When this category preference was last saved.
- `source`: Whether the visible value came from a saved preference or a default.

**Relationships**

- Belongs to one `User Profile`.
- Applies to zero or more future `Delivery Decision` records.

**Validation Rules**

- Category must be recognized or explicitly defaulted by the notification category registry.
- Turning both channels off means the category is off for that person.
- Preference changes apply only to delivery decisions evaluated after `updatedAtUtc`.

## Notification Category

Represents a user-controllable kind of notification.

**Fields**

- `category`: Stable key.
- `displayName`: Label shown in the profile preference UI.
- `defaultInAppEnabled`: Default in-app delivery value.
- `defaultEmailEnabled`: Default email delivery value.
- `triggerEvents`: Events that produce candidate notifications for the category.

**Category Values**

- `ItineraryChanges`: Trip edit, leg create/update/delete, event create/update/delete.
- `TripSharing`: Trip shared, trip permission changed, trip permission removed.

**Validation Rules**

- Defaults are used only when the person has no saved category preference.
- New categories must define defaults before they appear in the profile preference UI.

## Notification Trigger

Represents a successful domain change that can produce candidate notifications.

**Fields**

- `triggerType`: Specific event type, such as `TripUpdated`, `TripLegCreated`, `TripEventDeleted`, `TripShared`, `TripSharePermissionChanged`, or `TripShareRemoved`.
- `category`: Notification category for preference evaluation.
- `tripId`: Related trip.
- `actorUserId`: Person who performed the change.
- `affectedUserId`: Person directly affected, for sharing triggers.
- `occurredAtUtc`: When the successful change happened.
- `sourceEventKey`: Stable duplicate-suppression key for recipient/event uniqueness.

**Relationships**

- Produces one or more `Candidate Recipient` values.
- Produces zero or more persisted `Notification` values after preference evaluation.

**Validation Rules**

- Trigger is emitted only after the underlying mutation succeeds.
- `actorUserId` is excluded from itinerary-change candidate recipients.
- Sharing triggers include the affected user as candidate recipient even for permission removal.

## Candidate Recipient

Represents a person who should be evaluated for notification delivery.

**Fields**

- `userId`: Candidate recipient.
- `permissionLevel`: Current trip permission when applicable (`owner`, `viewer`, `collaborator`).
- `email`: Email address available for email delivery.
- `canOpenTrip`: Whether the recipient can still open the related trip.

**Relationships**

- Derived from trip owner/member access for itinerary changes.
- Derived from the affected sharing user for trip-sharing events.

**Validation Rules**

- Itinerary changes include current viewers/editors and exclude the actor.
- Sharing removal may produce a candidate recipient whose `canOpenTrip` is false after removal.

## Delivery Decision

Represents the result of applying a candidate recipient's preferences to a notification trigger.

**Fields**

- `recipientUserId`: Candidate recipient being evaluated.
- `category`: Trigger category.
- `inAppAllowed`: Result of preference evaluation for in-app delivery.
- `emailAllowed`: Result of preference evaluation for email delivery.
- `decision`: `Deliver`, `SuppressChannel`, or `SuppressAll`.
- `reason`: Defaulted, saved preference, disabled channel, category off, duplicate event, or unavailable preferences.
- `evaluatedAtUtc`: Evaluation timestamp.

**Relationships**

- Reads from `User Profile` / `Notification Preference`.
- May create a `Notification` and optional email delivery request.

**Validation Rules**

- No channel can deliver if that channel is disabled for the recipient/category.
- If all channels are disabled, no notification is delivered.
- Preference data must be evaluated before creating notification delivery records.
