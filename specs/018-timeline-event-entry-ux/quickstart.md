# Quickstart: Timeline Event Entry Experience Enhancements

**Feature**: 018-timeline-event-entry-ux
**Date**: 2026-07-10

A validation guide proving the feature works end to end. See [spec.md](spec.md) for requirements, [data-model.md](data-model.md) for view-state concepts, and the [contracts/](contracts/) for component/JS/form/icon details.

## Prerequisites

- .NET 10 SDK installed.
- The solution runs via Aspire: use the existing VS Code task **watch (Aspire hot reload)** or run the AppHost:
  ```pwsh
  dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
  ```
- A signed-in user who owns a trip that has at least one leg spanning several days (long enough that the timeline scrolls horizontally), plus a few events of different types.

## Scenario 1 — Scrolling updates the active (centered) date (P1)

1. Open the trip details page and view the timeline.
2. Scroll the timeline horizontally so a mid-trip day is centered in the viewport.
3. **Expected**: the header "Jump to date" label updates to the centered day, and that day is highlighted as active — without using the jump control.
4. Use the "Jump to date" control / next-day / previous-day and confirm the active date stays consistent with the centered day.

## Scenario 2 — Add event defaults start and end to the active date (P1)

1. With the timeline centered on a specific day (not the trip's first day), click **+ Add event** on a leg.
2. **Expected**: the event form opens with the start date set to the centered day and the end set to one hour after the start — before any typing.
3. Move the timeline to a different day, add another event, and confirm the new default reflects the newly centered day (not the previous one).
4. Open an existing event to edit it and confirm its saved dates are shown and **not** overwritten by the active date.

## Scenario 3 — End time reacts to start selection

1. In the Add-event form, change the start date/time.
2. **Expected**: the end date/time auto-adjusts to one hour after the new start.
3. Now edit the end directly, then change the start again.
4. **Expected**: after a manual end edit, the end no longer auto-follows the start (your end value is kept).

## Scenario 4 — Location as a validated, mappable address

1. In the event form, enter a place/address in Location (e.g. "Eiffel Tower, Paris").
2. **Expected**: the field is presented as an address and passes validation; a globe icon becomes available.
3. Enter an invalid value (e.g. "!!!").
4. **Expected**: inline validation flags it as not map-capable and the globe action is not offered; clearing the field is allowed (location is optional).
5. With a valid location, click the globe icon.
6. **Expected**: a map opens in a new browser tab centered on the entered location, and returning to the app leaves your unsaved event edits intact.

## Scenario 5 — Type icons on the timeline (P2)

1. Ensure the trip has items of each type (event, reservation, activity, reminder).
2. View the timeline.
3. **Expected**: each item shows an icon matching its type; two items of the same type show the same icon.
4. Edit an item's type and save.
5. **Expected**: the item's icon on the timeline updates to the new type. An item with an unknown/missing type shows a clear default icon (never iconless).

## Automated tests

- **bUnit** (`tests/TripPlanner.Web.Tests`, `Timeline/` suite): active-date update via the `[JSInvokable]` reporter; add-event start/end defaults from `CurrentDate`; reactive end (+1h until manual); location validation (empty valid, symbols invalid, place valid); globe link presence/absence and safe target; type-icon rendering per type plus default.
- **Playwright** (`tests/TripPlanner.E2E.Tests`): scrolling recenters and updates the active date; adding an event defaults to the active date; the globe opens a maps URL for a valid location.

Run the component tests:
```pwsh
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
```

## Success check (maps to Success Criteria)

- New event start matches the centered day before typing (SC-001, SC-002).
- Every timeline entry has a type-appropriate or default icon (SC-003, SC-004).
- A valid location reaches a map in one action; no map action without a location (SC-005, SC-006).
