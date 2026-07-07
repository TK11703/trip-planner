# Phase 0 Research: User Notifications

## Decision: Model notifications as recipient-owned records

**Rationale**: The feature requires user-specific visibility, unread counts, deletion, completion, email delivery, and optional trip links. A recipient-owned `notifications` model keeps authorization simple: every API operation filters by the signed-in user's identity, and shared events create one row per affected recipient.

**Alternatives considered**: A global event log with per-user projections was rejected for this feature because it adds projection complexity before there is a need for shared event analytics or fan-out replay.

## Decision: Use optional trip targeting rather than separate person-only and trip notification types

**Rationale**: Notifications can target a person only or jointly target a person and trip. A nullable trip relationship plus a target type lets the UI show a trip link only when present while keeping one list, one count, and one set of read/delete/complete operations.

**Alternatives considered**: Separate tables for person notifications and trip notifications were rejected because read state, deletion, completion, and email delivery would be duplicated across nearly identical models.

## Decision: Track actionable notification completion on the notification record

**Rationale**: Actionable notifications must record the date/time and person who completed the action and display that information with the notification. Storing completion metadata with the notification keeps list rendering and completion checks direct.

**Alternatives considered**: A separate action audit table was considered. It can be added later for history, but a single completion state is enough for this feature's requirement and avoids an extra join in the notification list.

## Decision: Use soft deletion per recipient

**Rationale**: Notifications belong to one recipient and can be deleted from that recipient's list. A `deleted_at` timestamp preserves audit/debug ability and avoids losing email-delivery or completion evidence immediately.

**Alternatives considered**: Hard deletion was rejected because it would remove completion and delivery history as soon as the recipient deletes the notification.

## Decision: Show the unread count in the account dropdown trigger and third menu option

**Rationale**: The current authenticated navigation surface is `NavMenu.razor`, where the account dropdown already contains Profile, a divider, and Sign out. The requested first visible count should live in the dropdown trigger beside the account identity, and the dropdown should insert Notifications as the third menu option while preserving Profile and Sign out.

**Alternatives considered**: A separate top-level navbar Notifications link was rejected because the user specifically requested the count first in the user's dropdown menu.

## Decision: Add a dedicated Notifications display route

**Rationale**: Clicking the dropdown menu option should open a notification display screen. A dedicated `/notifications` page can render the templated list, support read/delete/complete actions, and handle trip links without overloading the account dropdown.

**Alternatives considered**: Rendering the full notification list inside the dropdown was rejected because actionable completion, deletion, status details, and trip links need more space and clearer state feedback.

## Decision: Use API endpoints for counts, list, mark-read, delete, complete, and preferences

**Rationale**: The existing application uses authenticated Minimal API vertical slices with Blazor API clients. Keeping notifications behind API endpoints ensures server-side recipient filtering, trip-access validation, and consistent Dapper persistence.

**Alternatives considered**: Direct database access from the web project was rejected because it would bypass the existing API boundary and duplicate authorization logic.

## Decision: Use an email outbox abstraction for notification email delivery

**Rationale**: Email must not block in-app notification delivery. Recording a notification and an email delivery request transactionally allows the in-app notification to succeed even if email transport is unavailable, while a background dispatcher can retry or record failures.

**Alternatives considered**: Sending email inline during notification creation was rejected because email failure must not prevent in-app delivery.

## Decision: Keep notification preferences category-based with channel flags

**Rationale**: Preferences need to control which categories a person receives and whether each category goes to in-app, email, or both. Category/channel flags match the spec without requiring a rule engine.

**Alternatives considered**: A fully custom rules system was rejected as too broad for the current feature.
