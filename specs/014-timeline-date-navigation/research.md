# Phase 0 Research: Timeline Date Navigation

**Feature**: 014-timeline-date-navigation
**Date**: 2026-07-09

The specification contained no `[NEEDS CLARIFICATION]` markers. The user provided concrete design direction (a "Jump to date" control listing individual trip dates that scrolls the selected day to the left edge, replacing the header Add-leg/Add-event buttons, and moving Add-leg to a legs-column footer). Research below confirms how that direction maps onto the existing implementation.

## Decision 1: How to bring a date "into view" at the left edge

- **Decision**: Scroll the existing `.ttl-scroll` container horizontally to a computed pixel offset so the chosen day's first column aligns to the left edge of the scrollable track (just after the sticky leg-label column).
- **Rationale**: The timeline is a single horizontally scrollable grid with fixed geometry: each day is `SlotsPerDay * SlotWidthPx` pixels wide (`SlotsPerDay = 48`, `SlotWidthPx = 26`, i.e. 1248px/day). The left offset of day index `d` is `d * SlotsPerDay * SlotWidthPx`. Setting `scrollLeft` to that offset is O(1), needs no layout change, and satisfies "leftmost" placement from the user's guidance.
- **Alternatives considered**:
  - `Element.scrollIntoView()` on a per-day anchor — rejected: it centers or aligns to nearest edge inconsistently and can scroll the whole page vertically; less precise than an explicit `scrollLeft`.
  - Changing timeline zoom/granularity to fit more days — rejected: the spec explicitly keeps structure, zoom, and hour granularity unchanged.

## Decision 2: Smooth vs. instant scroll

- **Decision**: Use smooth horizontal scrolling (`scrollTo({ left, behavior: 'smooth' })`) with a graceful fallback to instant when reduced-motion is preferred.
- **Rationale**: Smooth scroll gives a clear sense of movement toward the target and keeps the current-position cue understandable, while staying well under the 3-second settle budget (SC-002). Respecting `prefers-reduced-motion` keeps accessibility intact.
- **Alternatives considered**: Instant jump only — acceptable but less orienting; smooth-with-fallback is a superset.

## Decision 3: Where the date list and scroll logic live

- **Decision**: Keep ownership inside `TripTimeline.razor`. Expose a public read-only date list (`TripDates`) and a public `ScrollToDateAsync(DateOnly)` method. `TripDetails.razor` renders the "Jump to date" control in the card header and calls these through its existing `@ref="_timeline"`.
- **Rationale**: The component already owns `_start`, `_end`, `_days`, the `_scrollEl` reference, and the JS interop lifecycle. Centralizing offset math there avoids duplicating geometry constants in the page and keeps `TripDetails` as a thin host. The `@ref` already exists, so no new wiring pattern is introduced.
- **Alternatives considered**:
  - Compute offsets in `TripDetails` and pass raw pixels to JS — rejected: leaks layout constants (`SlotWidthPx`, `SlotsPerDay`) out of the component.
  - Put the jump control fully inside the timeline body — rejected: the user specifically asked for it in the card header's top-right.

## Decision 4: Current-position indication

- **Decision**: Track the currently selected/jumped-to date in component state and highlight that date in the "Jump to date" list (e.g., an active/selected menu item and a check or emphasis). Optionally emphasize the matching day header column.
- **Rationale**: FR-003 requires the user to confirm where the timeline is positioned. Highlighting the chosen date in the control is the most direct, low-risk cue and is easy to test.
- **Alternatives considered**: Deriving position from scroll offset on every scroll event — rejected: unnecessary per-scroll work; the deterministic "last selected date" is sufficient and matches how navigation actions drive position.

## Decision 5: Date list scale for long trips

- **Decision**: Render the trip dates in a scrollable dropdown/menu (bounded max height) grouped for readability; the list is the exact set of dates from trip start to trip end inclusive.
- **Rationale**: The date list length equals `_days`, which for multi-week trips is still small (tens of items). A scrollable menu keeps a single-action jump for any date without paging (SC-001, SC-004).
- **Alternatives considered**: A native date picker constrained to the range — viable, but an explicit list of just the trip's dates better matches the "filter option that lists all individual dates" request and avoids selecting invalid days.

## Decision 6: Layout relocation of Add-leg / Add-event

- **Decision**: Remove the "Add leg" and "Add event" buttons from the timeline card header (`TripDetails.razor`) and place the "Jump to date" control there instead. Add an "Add leg" action as a footer row at the bottom of the sticky trip-legs label column inside `TripTimeline.razor`, shown only when `CanEdit` is true. "Add event" remains available via the existing per-leg "+ Add event" button.
- **Rationale**: Matches the user's explicit guidance, keeps editing affordances close to their context (legs column for adding legs), and frees the header for navigation. Reusing the existing `OpenCreateLegModal` handler avoids new logic; the footer surfaces an `OnAddLeg` callback the page already implements.
- **Alternatives considered**: Keep Add-leg in the header alongside Jump-to-date — rejected: user asked to consolidate and de-clutter the header.

## Decision 7: Accessibility and theming

- **Decision**: The "Jump to date" control and legs-column footer button use existing Bootstrap button/dropdown components with explicit `aria-label`s, are keyboard-operable (focusable, Enter/Space activation, arrow-key menu navigation), and rely on existing theme CSS variables for light/dark support.
- **Rationale**: Satisfies FR-012 and the assumption to reuse current branding/theme; Bootstrap dropdowns already provide keyboard semantics used elsewhere in the app (e.g., the trip actions dropdown in `TripDetails`).
- **Alternatives considered**: Custom popover — rejected: more code and accessibility risk than the established pattern.

## Open Questions

None. All spec requirements map to confirmed decisions above.
