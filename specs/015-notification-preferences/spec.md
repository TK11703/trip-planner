# Feature Specification: Notification Preferences

**Feature Branch**: `[015-notification-preferences]`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User description: "notifications are important. However there is a confusing aspect to notifications in the app right now. The user should be able to control their notification preferences. When notifications are being sent to a user, the user preferences should overrule the delivery. Additional planning details: the notification_preferences table works fine, but preferences should move or consolidate into the profile. Notifications should be generated for itinerary changes when any trip edit, new leg, updated leg, deleted leg, new event, updated event, or event deletion occurs. The person performing the action should not be notified, but anyone with view or edit permissions should be notified. Trip Sharing notifications should be generated when a person is shared a trip, when their permissions change, and when their permissions are removed."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manage My Notification Preferences (Priority: P1)

A signed-in person can view and change their notification preferences so they decide which kinds of notifications they receive and which delivery channels are allowed for each kind.

**Why this priority**: User control is the core need. Without a clear way to set preferences, delivery cannot meaningfully reflect the person's intent.

**Independent Test**: Can be fully tested by signing in as a person, opening notification preferences, changing the allowed delivery channels for at least one notification category, saving the changes, and confirming those choices remain visible after leaving and returning to the preferences view.

**Acceptance Scenarios**:

1. **Given** a signed-in person has not customized notification preferences, **When** they open notification preferences, **Then** they see the available notification categories with the default delivery choices for each category.
2. **Given** a signed-in person views a notification category, **When** they disable email delivery and save, **Then** that category shows email delivery as disabled for that person.
3. **Given** a signed-in person views a notification category, **When** they disable all delivery for that category and save, **Then** that category is shown as turned off for that person.
4. **Given** a signed-in person has saved notification preferences, **When** they return to the preferences view in a later session, **Then** their saved choices are still shown.

---

### User Story 2 - Preferences Override Notification Delivery (Priority: P1)

A person who has disabled a notification category or channel does not receive future notifications through the disabled delivery path, even when the event that creates the notification would otherwise send it.

**Why this priority**: This resolves the confusing behavior described in the request. Preference enforcement must happen at delivery time so user choices overrule default or event-driven delivery behavior.

**Independent Test**: Can be fully tested by disabling one delivery channel for a category, triggering a future notification in that category, and confirming the disabled channel is not used while any still-enabled channel behaves according to the person's preferences.

**Acceptance Scenarios**:

1. **Given** a person has disabled email delivery for a notification category, **When** a future event creates a notification in that category for that person, **Then** no email notification is delivered to that person for that event.
2. **Given** a person has left in-app delivery enabled for the same category, **When** a future event creates a notification in that category for that person, **Then** the in-app notification is still delivered.
3. **Given** a person has turned off a notification category entirely, **When** a future event creates a notification in that category for that person, **Then** the notification is not delivered to that person through any channel.
4. **Given** two people have different preferences for the same notification category, **When** the same event concerns both people, **Then** each person's delivery outcome follows only their own preferences.

---

### User Story 3 - Keep Defaults Predictable Until Changed (Priority: P2)

A person who has not changed notification preferences still receives important notifications according to clear defaults, so notification delivery remains useful without requiring setup.

**Why this priority**: Notifications remain important, and the product should continue to notify people unless they choose otherwise. Defaults prevent a blank or uncertain state for new and existing users.

**Independent Test**: Can be fully tested by using a person with no saved preference changes, triggering a notification, and confirming the notification follows the default delivery choices shown in preferences.

**Acceptance Scenarios**:

1. **Given** a person has no saved preferences for a category, **When** a notification in that category is sent, **Then** delivery follows the default choices for that category.
2. **Given** default choices are shown in preferences, **When** a person saves changes for one category, **Then** only that category changes for that person and other categories continue using defaults.
3. **Given** a new notification category becomes available, **When** a person opens preferences, **Then** the category appears with a sensible default delivery choice.

---

### User Story 4 - Understand Preference Effects (Priority: P3)

A person can understand what their notification choices mean before saving them, so they can reduce unwanted delivery without accidentally missing notifications they still care about.

**Why this priority**: The request calls out confusion. Clear preference presentation reduces accidental opt-outs and makes delivery behavior easier to trust.

**Independent Test**: Can be fully tested by reviewing the preferences view and confirming each category and channel clearly communicates whether future notifications will be delivered.

**Acceptance Scenarios**:

1. **Given** a person is editing preferences, **When** they review a notification category, **Then** the view makes clear which future delivery channels are enabled or disabled.
2. **Given** a person turns off all delivery for a category, **When** the choice is displayed before saving, **Then** the view makes clear that future notifications in that category will not be delivered.
3. **Given** a preference change is saved, **When** a confirmation is shown, **Then** it communicates that the change applies to future notification delivery.

---

### User Story 5 - Receive Relevant Trip Change Notifications (Priority: P2)

A person with access to a trip is notified when someone else changes the itinerary, so they can stay aware of edits to trip details, legs, and events without notifying the person who made the change.

**Why this priority**: Itinerary changes are one of the named notification categories that must obey preferences while still informing other trip participants.

**Independent Test**: Can be fully tested by giving two people access to a trip, having one person change the trip, a leg, or an event, and confirming only the other eligible person receives a notification if their preferences allow it.

**Acceptance Scenarios**:

1. **Given** a trip has multiple people with view or edit permission, **When** one person edits the trip, **Then** every other person with view or edit permission is considered for an itinerary-change notification.
2. **Given** a trip has multiple people with view or edit permission, **When** one person creates, updates, or deletes a leg, **Then** every other person with view or edit permission is considered for an itinerary-change notification.
3. **Given** a trip has multiple people with view or edit permission, **When** one person creates, updates, or deletes an event, **Then** every other person with view or edit permission is considered for an itinerary-change notification.
4. **Given** a person performs an itinerary change, **When** notifications are generated for that change, **Then** the person who performed the action is not notified.
5. **Given** an eligible recipient has disabled itinerary-change notifications, **When** an itinerary change occurs, **Then** their disabled preferences overrule delivery.

---

### User Story 6 - Receive Trip Sharing Notifications (Priority: P2)

A person is notified when trip access is shared with them, changed, or removed, so they understand how their trip access has changed.

**Why this priority**: Sharing and permission changes directly affect a person's ability to view or edit trip plans, and the request names these as required notification events.

**Independent Test**: Can be fully tested by sharing a trip with a person, changing their permission level, and removing their permission, then confirming the affected person is considered for trip-sharing notifications according to their preferences.

**Acceptance Scenarios**:

1. **Given** a trip owner shares a trip with another person, **When** the share succeeds, **Then** the newly shared person is considered for a trip-sharing notification.
2. **Given** a trip owner changes a person's trip permission, **When** the permission change succeeds, **Then** the affected person is considered for a trip-sharing notification.
3. **Given** a trip owner removes a person's trip permission, **When** the removal succeeds, **Then** the affected person is considered for a trip-sharing notification.
4. **Given** an affected person has disabled trip-sharing notifications, **When** sharing changes occur, **Then** their disabled preferences overrule delivery.

---

### Edge Cases

- A person disables email for a category after a notification has already been delivered by email.
- A person disables all delivery for a category and later turns one channel back on.
- A notification event affects multiple people with different preferences.
- A person has no saved preferences because they are new, migrated, or have never opened the preferences view.
- A notification category is added after some people already have saved preferences.
- A notification category is retired or no longer used while people still have saved preferences for it.
- A delivery attempt is prepared before a preference change but has not yet been sent.
- A person's preference data is temporarily unavailable at the moment delivery is evaluated.
- A person attempts to change preferences while signed out or while their session is no longer valid.
- A person who performed a trip change also has owner, view, or edit access and would otherwise be part of the recipient set.
- A trip change affects a person whose trip access is removed before delivery is evaluated.
- A permission removal notification must be delivered even though the recipient no longer has access to open the trip.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a signed-in person to view their notification preferences.
- **FR-002**: The system MUST show notification preferences by notification category and delivery channel.
- **FR-003**: The system MUST support at least the currently available delivery channels: in-app and email.
- **FR-004**: The system MUST allow a signed-in person to enable or disable each available delivery channel for each notification category.
- **FR-005**: The system MUST allow a signed-in person to turn off a notification category entirely by disabling all delivery channels for that category.
- **FR-006**: The system MUST save each person's notification preferences so the choices persist across sessions.
- **FR-007**: The system MUST show default delivery choices for categories where a person has not saved a preference.
- **FR-008**: The system MUST apply default delivery choices until a person saves a different preference for that category.
- **FR-009**: The system MUST evaluate the intended recipient's preferences before delivering any future notification through any channel.
- **FR-010**: The system MUST prevent delivery through a channel that the intended recipient has disabled for the notification's category.
- **FR-011**: The system MUST prevent all delivery for a notification category that the intended recipient has turned off entirely.
- **FR-012**: The system MUST apply preferences independently per person when the same event concerns multiple people.
- **FR-013**: The system MUST apply preference changes only to notifications whose delivery is evaluated after the change is saved.
- **FR-014**: The system MUST NOT remove, rewrite, or hide notifications that were already delivered before a preference change.
- **FR-015**: The system MUST make preference status understandable before saving, including when a category will no longer deliver notifications through any channel.
- **FR-016**: The system MUST confirm when preference changes have been saved.
- **FR-017**: The system MUST preserve a person's existing preferences when new notification categories are added, while applying defaults for the new categories.
- **FR-018**: The system MUST handle unavailable preference data by avoiding delivery through uncertain channels until the person's preferences can be evaluated.
- **FR-019**: The system MUST require the person to be signed in before viewing or changing notification preferences.
- **FR-020**: The system MUST ensure one person's preference choices cannot change another person's notification delivery.
- **FR-021**: The system MUST expose notification preference controls as part of the user's profile experience rather than requiring a separate preference surface.
- **FR-022**: The system MUST preserve compatibility with existing notification preference data while consolidating preference management into the profile.
- **FR-023**: The system MUST support an itinerary-change notification category covering trip edits, new legs, updated legs, deleted legs, new events, updated events, and deleted events.
- **FR-024**: The system MUST consider all people with current view or edit permission on the trip as recipients for itinerary-change notifications.
- **FR-025**: The system MUST exclude the person who performed an itinerary change from receiving a notification for that same change.
- **FR-026**: The system MUST apply each candidate recipient's preferences before delivering itinerary-change notifications.
- **FR-027**: The system MUST support a trip-sharing notification category covering a trip being shared with a person, a person's trip permission changing, and a person's trip permission being removed.
- **FR-028**: The system MUST consider the affected person as the recipient for trip-sharing notifications.
- **FR-029**: The system MUST apply the affected person's preferences before delivering trip-sharing notifications.
- **FR-030**: The system MUST ensure notifications are generated only after the underlying trip or sharing change succeeds.

### Key Entities *(include if feature involves data)*

- **Notification Preference**: A person's saved choice for a notification category, including which delivery channels are enabled or disabled.
- **User Profile**: The signed-in person's profile data and preferences; notification preference controls are consolidated here for viewing and editing.
- **Notification Category**: A kind of notification that can be controlled by preference, such as trip activity, sharing updates, or actionable reminders.
- **Delivery Channel**: A way a notification can reach a person, currently in-app and email.
- **Delivery Decision**: The outcome of evaluating a notification for a person against that person's preferences before delivery occurs.
- **Default Preference**: The delivery choice used for a category when a person has not saved their own preference.
- **Notification Trigger**: A successful trip itinerary or sharing change that creates one or more candidate notification deliveries.
- **Candidate Recipient**: A person who should be evaluated for notification delivery before preferences are applied.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 95% of users can find and save notification preferences for a category within 60 seconds without external guidance.
- **SC-002**: 100% of future notification deliveries respect the intended recipient's saved preferences at the time delivery is evaluated.
- **SC-003**: 0% of notifications are delivered through a channel the recipient has disabled for that notification category.
- **SC-004**: 0% of future notifications are delivered for categories the recipient has turned off entirely.
- **SC-005**: When one event concerns multiple people, 100% of delivery outcomes are evaluated separately for each recipient.
- **SC-006**: 100% of people with no saved preferences receive notifications according to the visible defaults for each category.
- **SC-007**: 100% of saved preference changes remain visible after the person leaves and returns to the preferences view.
- **SC-008**: 90% of users understand from the preferences view whether a category will deliver in-app, by email, both, or neither.
- **SC-009**: 100% of successful itinerary changes generate candidate notifications for eligible viewers/editors other than the acting person.
- **SC-010**: 100% of trip sharing, permission change, and permission removal events generate candidate notifications for the affected person.

## Assumptions

- Notification categories and the in-app and email delivery channels come from the existing notifications feature rather than introducing unrelated communication channels.
- Notification preference management is consolidated into the user profile experience; existing notification preference storage may be reused or migrated as an implementation detail.
- Default preferences continue to deliver notifications through both in-app and email for existing categories unless a category-specific default is intentionally defined during planning.
- Preference changes affect future delivery decisions only; they do not retract emails already sent or remove in-app notifications already delivered.
- User-controllable notification categories must obey preferences. Any future legally required, security-critical, or administrative messages that cannot be opted out of must be specified separately and clearly distinguished from these preferences.
- If preference data cannot be evaluated at delivery time, protecting the user's saved intent is more important than maximizing delivery volume.
