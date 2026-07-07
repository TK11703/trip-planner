# UI Contract: User Notifications

## Account Dropdown Trigger

Surface: `src/TripPlanner.Web/Components/Layout/NavMenu.razor`

- The unread notification count is first visible in the signed-in user's dropdown trigger.
- The count appears as a compact number inside a circle.
- The badge is hidden or absent when the unread count is zero.
- The badge text is accessible to screen readers as the unread notification count.

## Account Dropdown Menu

- The menu includes a third option named `Notifications`.
- The Notifications option shows the same circle counter used by the dropdown trigger.
- Selecting Notifications navigates to `/notifications`.
- Existing Profile and Sign out options remain available.

## Notifications Display Screen

Route: `/notifications`

The screen presents a templated list of the signed-in user's notifications ordered newest to oldest.

Each row/card shows:

- Title.
- Message.
- Created date/time.
- Awareness vs actionable type.
- Read/unread state.
- Delete command.
- For trip-related notifications: a link to the related trip.
- For person-only notifications: no trip link.
- For actionable pending notifications: a Complete action.
- For completed actionable notifications: completed date/time and completing person.
- For email-configured notifications: delivery status if available.

## Awareness Notification Behavior

- Awareness notifications do not show a Complete action.
- Awareness notifications can be deleted.
- Opening or acknowledging an awareness notification marks it read.

## Actionable Notification Behavior

- Pending actionable notifications show a Complete action.
- Completing an actionable notification updates the row/card to show completion date/time and completing person.
- Completed actionable notifications no longer show the pending Complete action.
- Actionable notifications can be deleted whether pending or completed.

## Empty and Error States

- Empty list: show that there is nothing to catch up on.
- Deleted/inaccessible trip link: show that the trip is no longer available to the user.
- Email failure: keep the notification visible in-app and show non-blocking delivery status only where useful.
