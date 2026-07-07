# Feature Specification: User Notifications

**Feature Branch**: `[011-notifications]`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "Notifications help us become aware of things that need to be done, happened while we were away, or serve as general awareness to changes. Notifcations should be user specific and should be both for awareness and actionable. Users need to know about the notifications in the app and in email. Additional planning details: notification count should first be visible in the user's dropdown menu as a number inside a circle; the dropdown menu should include a third Notifications option with the same circle counter; clicking it should open a notification display screen with a templated list. Awareness notifications can be deleted. Actionable notifications can be completed, recording the completion date/time and person, and can also be deleted. Notifications may target only a person or both a trip and a person; trip-related notifications must link to the trip."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See My Notifications In the Application (Priority: P1)

A signed-in person opens the application and sees the notifications meant for them — things that happened while they were away, changes they should be aware of, and items that need their attention — so they can quickly catch up on what matters to them without hunting through the app.

**Why this priority**: In-app awareness is the core of the feature. Without a person being able to see their own notifications when they return to the app, none of the other capabilities (acting on them, receiving them by email, tuning preferences) have anything to build on. This is the smallest slice that delivers the feature's value.

**Independent Test**: Can be fully tested by triggering an event that concerns a specific person, signing in as that person, and confirming a notification for that event appears in their notification list with a clear indication that it is unread — while a different signed-in person does not see it.

**Acceptance Scenarios**:

1. **Given** a signed-in person with one or more unread notifications, **When** they view the user dropdown trigger, **Then** they see the unread notification count as a number inside a circle.
2. **Given** a signed-in person opens the user dropdown menu, **When** the menu is displayed, **Then** a third menu option named Notifications is shown with the same circle counter.
3. **Given** a signed-in person selects the Notifications menu option, **When** navigation completes, **Then** they see a notification display screen with a templated list of their notifications.
4. **Given** a signed-in person, **When** they open their notification list, **Then** they see their notifications ordered from newest to oldest, each showing what happened and when.
5. **Given** a person viewing a notification that they had not seen before, **When** they open or acknowledge it, **Then** it is marked as read and no longer counts toward their unread total.
6. **Given** two different people, **When** each opens their notification list, **Then** each person only sees notifications intended for them and never another person's notifications.
7. **Given** a person with no notifications, **When** they open their notification list, **Then** they see a clear empty state indicating there is nothing to catch up on.

---

### User Story 2 - Act on a Notification That Needs Attention (Priority: P2)

A person receives an actionable notification — something that needs to be done, such as reviewing a trip that was shared with them or responding to a change — and can go directly from the notification to the place where they take that action, so awareness turns into completion.

**Why this priority**: The request emphasizes that notifications should be "both for awareness and actionable." Once a person can see their notifications (User Story 1), being able to act on the ones that need attention is the next most valuable step, turning notifications into a way to get things done rather than just a log of events.

**Independent Test**: Can be fully tested by generating an actionable notification for a person, then confirming that opening it takes them to the relevant item in the application and that the notification reflects whether the action still needs attention.

**Acceptance Scenarios**:

1. **Given** an actionable notification, **When** the person opens it, **Then** they are taken to the specific item or place in the application where the related action can be taken.
2. **Given** an actionable notification that still needs attention, **When** an authorized person completes it, **Then** the notification records the completion date/time and the person who completed it.
3. **Given** a completed actionable notification, **When** a person views it, **Then** the completion date/time and completing person are displayed with the notification.
4. **Given** an informational (awareness-only) notification, **When** the person views it, **Then** it is presented as informational and does not imply an action is required.
5. **Given** an awareness notification, **When** the person deletes it, **Then** it is removed from their notification list.
6. **Given** an actionable notification, **When** the person deletes it, **Then** it is removed from their notification list whether or not it was completed.
7. **Given** the item a notification refers to no longer exists or is no longer accessible to the person, **When** they open the notification, **Then** the system explains that the item is no longer available instead of leading to a broken or empty destination.

---

### User Story 3 - Follow Trip-Related Notification Links (Priority: P2)

A person receives a notification that is related to a trip and can use the notification to open that trip, while notifications that are only about the person do not pretend to have a trip destination.

**Why this priority**: Notifications can be targeted to a person alone or to a person in the context of a trip. Trip-linked notifications need a clear route back to the relevant trip so users can inspect the item that changed or needs attention.

**Independent Test**: Can be fully tested by creating one person-only notification and one trip-related notification for the same person, confirming the trip-related notification links to the correct trip and the person-only notification has no trip link.

**Acceptance Scenarios**:

1. **Given** a notification related to a trip and a person, **When** the person views the notification, **Then** it includes a link to view that trip.
2. **Given** a notification targeted only to a person, **When** the person views the notification, **Then** it does not show a trip link.
3. **Given** a trip-related notification, **When** the person follows the trip link, **Then** the trip opens only if the person currently has access to that trip.
4. **Given** a trip-related notification for a trip the person can no longer access, **When** the person follows the link, **Then** the system explains that the trip is no longer available to them.

---

### User Story 4 - Receive Notifications by Email (Priority: P2)

A person who is not currently in the application still becomes aware of important notifications because they also arrive in their email, so they do not miss things that happened while they were away.

**Why this priority**: The request explicitly states people need to know about notifications "in the app and in email." Email reaches people when they are away from the application, which is one of the stated purposes ("happened while we were away"). It depends on the underlying notification concept from User Story 1 but delivers value across a second channel.

**Independent Test**: Can be fully tested by triggering a notification for a person and confirming that an email addressed to that person is produced containing the same essential information as the in-app notification.

**Acceptance Scenarios**:

1. **Given** an event that produces a notification for a person, **When** the notification is created, **Then** an email conveying the same essential information is sent to that person's email address.
2. **Given** an email notification, **When** the person reads it, **Then** it clearly identifies what happened and, for actionable notifications, how to get to the related item in the application.
3. **Given** a person whose email address is missing or invalid, **When** a notification is created for them, **Then** the in-app notification is still delivered and the email failure does not prevent it.
4. **Given** an email notification is sent, **When** the person later opens the application, **Then** the same notification is present in their in-app list so the two channels stay consistent.

---

### User Story 5 - Control Which Notifications I Receive and Where (Priority: P3)

A person adjusts their notification preferences — choosing which kinds of notifications they receive and whether each arrives in the app, by email, or both — so they get the awareness they want without unwanted noise.

**Why this priority**: Once notifications flow through both channels, giving people control over volume and channel prevents fatigue and respects individual preferences. It is a refinement on top of the core delivery capabilities, so it has the lowest priority here.

**Independent Test**: Can be fully tested by having a person turn off email for a category of notification, triggering an event in that category, and confirming the in-app notification still appears while no email is sent.

**Acceptance Scenarios**:

1. **Given** a person viewing their notification preferences, **When** they turn off email delivery for a category of notification, **Then** future notifications in that category still appear in the app but do not generate an email.
2. **Given** a person viewing their notification preferences, **When** they turn off a category entirely, **Then** future notifications in that category are no longer delivered to them by any channel.
3. **Given** a person who has not changed any preferences, **When** notifications are produced for them, **Then** they are delivered according to sensible defaults for both in-app and email.
4. **Given** a person changes a preference, **When** the change is saved, **Then** it applies to notifications produced after the change and does not alter notifications they already received.

---

### Edge Cases

- A person accumulates a very large number of unread notifications over a long absence.
- The same underlying event would otherwise notify a person more than once (duplicate suppression).
- The item a notification refers to is deleted or made inaccessible after the notification was created.
- A person's email address is missing, invalid, or email delivery fails temporarily.
- A person marks all notifications as read at once.
- A person deletes an awareness notification.
- A person deletes an actionable notification before or after completion.
- An actionable notification is completed twice or completed by a person who is not allowed to complete it.
- Notifications are generated for a person who has never signed in or no longer has an account.
- An event affects several people at once and each must receive their own user-specific notification.
- A person turns off a notification category and later turns it back on.
- Notifications older than the retention window need to be cleared without affecting recent ones.
- A person acts on the item directly (outside the notification), leaving an actionable notification whose action is already complete.
- A trip-related notification points to a trip that was deleted or to which the person no longer has access.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST create notifications that are specific to an individual person, so each notification belongs to exactly one recipient.
- **FR-002**: The system MUST show a signed-in person only the notifications intended for them and never another person's notifications.
- **FR-003**: The system MUST generate a notification when a relevant event occurs that a person should be made aware of, including things that need to be done, things that happened while they were away, and changes relevant to them.
- **FR-004**: The system MUST classify each notification as either awareness (informational only) or actionable (something that needs to be done).
- **FR-005**: The system MUST present, for each notification, what happened and when it happened.
- **FR-006**: The system MUST indicate to a signed-in person how many unread notifications they have.
- **FR-007**: The system MUST show the unread notification count first in the signed-in user's dropdown trigger as a number inside a circle.
- **FR-008**: The system MUST add a Notifications option as the third menu option in the signed-in user's dropdown menu, also showing the unread count inside a circle.
- **FR-009**: The system MUST navigate the person to a notification display screen when they select the Notifications dropdown option.
- **FR-010**: The system MUST let a person view their notifications ordered from newest to oldest in a templated list.
- **FR-011**: The system MUST allow a person to mark a notification as read, and MUST stop counting read notifications toward the unread total.
- **FR-012**: The system MUST allow a person to mark all of their notifications as read at once.
- **FR-013**: The system MUST let a person open an actionable notification and be taken to the specific item or place in the application where the related action can be taken.
- **FR-014**: The system MUST handle the case where the item a notification refers to no longer exists or is no longer accessible, by informing the person instead of leading to a broken destination.
- **FR-015**: The system MUST deliver notifications to a person's email in addition to the in-app notification.
- **FR-016**: The system MUST ensure an email notification conveys the same essential information as the corresponding in-app notification, including how to reach the related item for actionable notifications.
- **FR-017**: The system MUST still deliver the in-app notification when email delivery fails or the person's email address is missing or invalid.
- **FR-018**: The system MUST keep the in-app and email channels consistent, so a notification delivered by email is also present in the person's in-app list.
- **FR-019**: The system MUST allow a person to control which categories of notifications they receive and whether each is delivered in the app, by email, or both.
- **FR-020**: The system MUST apply sensible default delivery settings for a person who has not changed their preferences, covering both in-app and email.
- **FR-021**: The system MUST apply preference changes only to notifications produced after the change, without altering notifications already delivered.
- **FR-022**: The system MUST avoid delivering duplicate notifications to a person for the same underlying event.
- **FR-023**: The system MUST persist notifications and their read/unread status so they remain available across sessions until read, dismissed, or removed by retention.
- **FR-024**: The system MUST generate a separate, user-specific notification for each affected person when a single event concerns multiple people.
- **FR-025**: The system MUST provide an empty state when a person has no notifications.
- **FR-026**: The system MUST allow a person to delete awareness notifications from their notification list.
- **FR-027**: The system MUST allow a person to delete actionable notifications from their notification list.
- **FR-028**: The system MUST allow an actionable notification to be completed.
- **FR-029**: The system MUST record the completion date/time and completing person when an actionable notification is completed.
- **FR-030**: The system MUST display the completion date/time and completing person on completed actionable notifications.
- **FR-031**: The system MUST support notifications targeted to a person only.
- **FR-032**: The system MUST support notifications related jointly to a person and a trip.
- **FR-033**: The system MUST provide a link to the related trip for trip-related notifications.
- **FR-034**: The system MUST validate current trip access before showing a trip from a trip-related notification link.

### Key Entities *(include if feature involves data)*

- **Notification**: A user-specific record that a relevant event occurred. It carries what happened, when it happened, its category, whether it is awareness or actionable, its read/unread status, optional trip relationship, optional action-completion details, and the delivery channels used.
- **Recipient**: The individual person a notification belongs to; a notification is always addressed to exactly one recipient, identified by their application account.
- **Trigger Event**: The occurrence in the application that causes one or more notifications to be created (for example, something needing attention, an activity that happened while a person was away, or a change relevant to them).
- **Notification Category**: A grouping of notifications by kind, used to let a person choose what they receive and through which channels.
- **Notification Preference**: A person's settings that determine which categories of notifications they receive and whether each is delivered in the app, by email, or both.
- **Delivery Channel**: The means by which a notification reaches a person — in-app and email.
- **Notification Completion**: The completion state for an actionable notification, including whether it is complete, when it was completed, and which person completed it.
- **Notification Target**: The scope of the notification: person-only or person-plus-trip.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A signed-in person can find and open their notifications and identify how many are unread within 10 seconds of entering the application.
- **SC-002**: 100% of notifications are visible only to their intended recipient and never to any other person.
- **SC-003**: 100% of actionable notifications lead the person to the correct related item, or clearly explain that the item is no longer available.
- **SC-004**: 95% of notifications that are configured for email are delivered to the recipient's email within 5 minutes of the triggering event.
- **SC-005**: When email delivery fails, 100% of the corresponding in-app notifications are still delivered.
- **SC-006**: The in-app and email versions of a notification match on essential information 100% of the time.
- **SC-007**: When a person turns off a category or channel, 0% of future notifications in that category are delivered through the disabled channel.
- **SC-008**: No person receives more than one notification for the same underlying event.
- **SC-009**: A person can distinguish awareness notifications from actionable ones without external guidance in 100% of cases.
- **SC-010**: 100% of completed actionable notifications display the completion date/time and completing person.
- **SC-011**: 100% of trip-related notifications include a trip link, and 100% of person-only notifications omit trip links.

## Assumptions

- People are identified by their existing application account and sign-in identity (as established in features 001–002); no separate account or address book is introduced for notifications, and a person's email address is the one associated with their account.
- Notifications extend existing application capabilities: relevant events come from features already in the product (for example, trip sharing and collaboration in feature 010, and trip/leg/event activity in features 006–009), rather than introducing new event sources of their own.
- "Awareness" notifications are informational and require no response; "actionable" notifications point to a specific place in the application where the person can take the needed action and can later show completion details.
- Email notifications are sent per notification (near real time) rather than as batched digests; digest or scheduled-summary email is out of scope for this feature.
- Sensible defaults deliver notifications through both in-app and email until a person changes their preferences; the exact default per category can be refined during planning.
- A reasonable retention window applies to notifications so old items are eventually cleared; the exact duration follows standard practice and can be set during planning.
- Real-time push to a closed application (such as mobile push or browser push) is out of scope; in-app notifications are seen when the person is in the application, and email covers awareness while away.
- Localization, quiet hours, and advanced scheduling of notifications are out of scope for this feature.
- Actual email transport (the sending service) is assumed to be available to the application; selecting and configuring that service is an implementation concern handled during planning.
