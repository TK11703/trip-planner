# Quickstart & Validation: Timeline Date Navigation

**Feature**: 014-timeline-date-navigation
**Date**: 2026-07-09

This guide validates that a long trip timeline can be navigated to a specific date without extensive scrolling. See [spec.md](spec.md) for requirements, [data-model.md](data-model.md) for view-state, and [contracts/](contracts/) for the component and JS interop surfaces.

## Prerequisites

- .NET 10 SDK installed.
- Local PostgreSQL available via Aspire orchestration (default for this repo).
- A signed-in user who owns (or can edit) a trip. For a meaningful test, use a trip spanning **at least 10 days** so the timeline is wider than the viewport.

## Run the app

```powershell
# From the repository root — starts the Aspire AppHost (API, Web, database)
dotnet run --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Then open the Web front end from the Aspire dashboard and navigate to a trip:

```text
/trips/{TripId}
```

## Manual validation scenarios

### Scenario 1 — Jump directly to a date (User Story 1 / P1)

1. Open a trip that requires horizontal scrolling in the timeline card.
2. In the timeline card header (top-right), open the **Jump to date** control.
3. Confirm the control lists **every date** within the trip range and only those dates.
4. Select a date roughly in the middle of the trip.
5. **Expected**: the timeline scrolls so the selected day is at the **left edge** of the scroll area, and the selected date is highlighted as the current position. Settles in under 3 seconds.

### Scenario 2 — Step day by day (User Story 2 / P2)

1. With a date selected, use the **Next day** control repeatedly.
2. **Expected**: the timeline advances exactly one day each time and stops at the trip's last day (boundary is communicated, no movement past the end).
3. Use the **Previous day** control back to the first day.
4. **Expected**: it stops at the trip start and communicates the boundary.

### Scenario 3 — Orientation shortcuts (User Story 3 / P3)

1. Use **Trip start** and **Trip end** shortcuts.
2. **Expected**: the timeline lands on the first and last days respectively.
3. If the trip's range includes today, use **Today**.
4. **Expected**: the timeline scrolls to the current date. If today is outside the range, the **Today** shortcut is unavailable/inapplicable.

### Scenario 4 — Layout relocation

1. Confirm the header no longer shows **Add leg** / **Add event** buttons; it shows the **Jump to date** control instead.
2. Confirm **Add event** is still available on each leg row (the existing "+ Add event" button).
3. Scroll to the bottom of the trip-legs column and confirm an **Add leg** footer button (visible only when you can edit); activating it opens the create-leg modal.

### Scenario 5 — Accessibility & edge cases

1. Tab to the **Jump to date** control and operate it with the keyboard (open, arrow to a date, Enter to select).
2. Toggle light/dark mode and confirm the new controls follow the theme.
3. Open a **single-day** trip and confirm navigation stays on the one day with boundaries reached immediately.
4. Resize the window and confirm a subsequent date jump still brings the intended day into view.

## Automated tests

```powershell
# Component (bUnit) tests
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

# End-to-end (Playwright) tests
dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj
```

Suggested coverage:
- **bUnit**: the Jump-to-date control renders one entry per trip date; the current date is highlighted; the "Add leg" footer appears only when editing; boundary states disable/annotate next/previous at the ends; the "Today" shortcut is hidden when today is out of range.
- **Playwright**: on a multi-day trip, selecting a mid-trip date leaves that day at the left edge of the scroll container (assert `scrollLeft` matches the expected day offset); next/previous move one day and clamp at boundaries.

## Success signals

- Reaching any date takes a single action with no manual horizontal scrolling (SC-001, SC-004).
- The current position is identifiable at a glance (SC-003).
- Day stepping never leaves the trip range (SC-005).
