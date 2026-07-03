# UI Surface Contract: Modern UI Refresh

This contract lists the refreshed surfaces and observable outcomes expected from each.

## Global shell and navigation

- Uses the Adventurous explorer main graphic/icon in both prominent and compact placements.
- Shows current location clearly.
- Provides access to landing/home, trips, FAQ, about, sign-in/sign-out, and theme selection where appropriate.
- Preserves keyboard, touch, and screen reader operation.
- Applies selected light/dark mode consistently.

## Landing page

- Communicates trip-planning value and brand personality immediately.
- Presents a strong first impression, main graphic/icon, clear hierarchy, and prominent next action.
- Works for unauthenticated visitors using device/browser color setting by default.
- Does not require trip data to appear polished and useful.

## Recent/all trips

- Preserves existing personal trip listing behavior and ownership boundaries.
- Keeps empty and populated states easy to scan.
- Uses branded empty state with a clear next step.
- Maintains responsive cards/lists without horizontal scrolling.

## Trip detail and itinerary

- Preserves existing trip overview, planning actions, trip leg/item workflows, and user-friendly errors.
- Keeps overview, actions, itinerary content, and calendar content visually cohesive but distinguishable.
- Handles long names/text and many itinerary items without breaking layout.

## Calendar

- Preserves existing calendar planning behavior.
- Improves clarity for date ranges, selected items, empty periods, overlapping items, and dense itinerary information.
- Provides accessible keyboard/focus and non-color cues for selected/important states.
- Remains usable on phone/tablet viewports.

## FAQ and about

- Remain public informational surfaces.
- Use the same brand identity, color roles, typography tone, and responsive behavior as the rest of the app.
- Do not expose personal trip data.

## Modal-related flows and forms

- Preserve existing create/edit expectations and validation behavior.
- Use refreshed spacing, hierarchy, focus indicators, feedback colors, and non-color validation cues.
- Remain operable with keyboard and touch.

## Loading, empty, validation, denied, and unavailable states

- Match the Adventurous explorer brand direction.
- Provide recovery guidance where appropriate.
- Avoid revealing private trip/account data when access is denied or unauthenticated.
- Remain readable and distinct in light and dark modes.

## Theme selection behavior

- Signed-in travelers can select light or dark mode.
- The selected mode is saved per account and restored after sign-out/sign-in and on another device/client.
- Saved preference applies only to the authenticated account.
- If no saved preference exists, the UI follows device/browser color setting.
- Unauthenticated visitors follow device/browser color setting by default; any temporary visitor behavior must not be treated as account persistence.
