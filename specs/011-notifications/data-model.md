# Data Model: User Notifications

## Notification

Represents one notification addressed to exactly one recipient.

**Fields**:

- `NotificationId`: Unique identifier.
- `RecipientUserId`: Application identity of the person who owns the notification.
- `Category`: Notification grouping used for preferences and display.
- `Kind`: `Awareness` or `Actionable`.
- `TargetType`: `Person` or `Trip`.
- `RelatedTripId`: Optional trip identifier; present only when `TargetType` is `Trip`.
- `Title`: Short display text for list and email subject/context.
- `Message`: Human-readable notification body.
- `CreatedAt`: Date/time the notification was created.
- `ReadAt`: Optional date/time the recipient marked or opened the notification as read.
- `DeletedAt`: Optional date/time the recipient deleted the notification from their list.
- `ActionStatus`: `NotApplicable`, `Pending`, or `Completed`.
- `CompletedAt`: Optional date/time an actionable notification was completed.
- `CompletedByUserId`: Optional application identity of the person who completed the actionable notification.
- `SourceEventKey`: Stable deduplication key for the underlying event.
- `InAppDeliveredAt`: Date/time the notification became available in the app.
- `EmailDeliveryStatus`: `NotRequested`, `Pending`, `Sent`, `Failed`, or `Suppressed`.
- `EmailRequestedAt`: Optional date/time email delivery was requested.
- `EmailSentAt`: Optional date/time email was sent.
- `EmailFailureReason`: Optional delivery failure summary.

**Validation Rules**:

- `RecipientUserId`, `Category`, `Kind`, `TargetType`, `Title`, `Message`, `CreatedAt`, and `SourceEventKey` are required.
- `RelatedTripId` is required when `TargetType` is `Trip` and must be absent when `TargetType` is `Person`.
- `ActionStatus` must be `NotApplicable` for awareness notifications.
- `ActionStatus` must be `Pending` or `Completed` for actionable notifications.
- `CompletedAt` and `CompletedByUserId` are required when `ActionStatus` is `Completed` and absent otherwise.
- `(RecipientUserId, SourceEventKey)` must be unique to prevent duplicate notifications for the same recipient and event.
- Deleted notifications are excluded from normal list/count responses.

**Relationships**:

- Belongs to one recipient.
- Optionally references one trip.
- May produce zero or one email delivery request.

**State Transitions**:

- Read state: `Unread` -> `Read` when `ReadAt` is set.
- Deletion state: `Visible` -> `Deleted` when `DeletedAt` is set.
- Action state: `Pending` -> `Completed` when completion metadata is recorded.

## NotificationPreference

Represents one person's delivery settings for one notification category.

**Fields**:

- `UserId`: Application identity of the person who owns the preference.
- `Category`: Notification category.
- `InAppEnabled`: Whether future notifications in the category appear in the app.
- `EmailEnabled`: Whether future notifications in the category send email.
- `UpdatedAt`: Date/time the preference was last changed.

**Validation Rules**:

- `(UserId, Category)` must be unique.
- At least one default preference row or default policy must exist for each supported category.
- Preference changes apply only to notifications created after `UpdatedAt`.

## EmailDeliveryRequest

Represents email work associated with a notification.

**Fields**:

- `EmailDeliveryRequestId`: Unique identifier.
- `NotificationId`: Notification to email.
- `RecipientUserId`: Recipient identity copied for filtering and diagnostics.
- `RecipientEmail`: Email address at the time delivery was requested.
- `Status`: `Pending`, `Sent`, `Failed`, or `Suppressed`.
- `AttemptCount`: Number of send attempts.
- `LastAttemptAt`: Optional date/time of the most recent attempt.
- `SentAt`: Optional date/time of successful delivery.
- `FailureReason`: Optional failure summary.

**Validation Rules**:

- Must reference an existing notification.
- Missing or invalid email addresses produce `Suppressed` or `Failed` email status without preventing the notification from existing in-app.

## NotificationCategory

Represents an allowed category used for display and preferences.

**Fields**:

- `Category`: Stable category key.
- `DisplayName`: User-facing category name.
- `DefaultInAppEnabled`: Default in-app delivery setting.
- `DefaultEmailEnabled`: Default email delivery setting.

## Recipient

Represents the existing application account identity used for notification ownership.

**Fields**:

- `UserId`: Application identity.
- `DisplayName`: Optional name for completion display.
- `Email`: Optional email address used for email delivery.

## Trip Relationship

A notification may reference a trip when it is jointly targeted to a person and trip. The trip link must use existing trip access checks. The notification list may show the link, but opening the trip must deny access if the person is no longer the owner or a current member.
