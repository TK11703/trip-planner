# Data Model: Trip Leg and Event Timeline Adjustments

## Overview

This feature is primarily a presentation and interaction adjustment to the existing trip timeline. It does not require new persisted entities or schema changes. The timeline already returns trip legs with nested timeline items, so event counts and row-level interactions can be derived from existing data.

## Entity: Timeline Calendar

**Purpose**: Per-trip chronological view that renders trip legs as resource rows and events as bars positioned within those rows.

**Existing fields used**:

- `TripId`: Identifies the trip being displayed.
- `StartDate` / `EndDate`: Calendar range boundaries.
- `SlotMinutes`: Timeline slot size used for click-to-time selection.
- `Legs`: Ordered trip-leg rows.
- `UnassignedItems`: Events without a leg assignment.

**Derived presentation state**:

- `TrackWidthPx`: Computed from date range, slots per day, and slot width.
- `RowClickableArea`: The portion of each leg lane not covered by event bars and available for selecting a time slot.

**Validation rules**:

- Timeline data is owner-scoped by the existing API and repository flow.
- Slot selection must clamp to the displayed timeline range.

## Entity: Trip Leg Timeline Row

**Purpose**: Sticky left label plus horizontal lane for one trip leg.

**Existing fields used**:

- `TripLegId`: Stable row identity and event creation target.
- `Title`: Primary label.
- `Origin` / `Destination`: Secondary label when present.
- `StartLocal` / `EndLocal`: Leg time range drawn on the calendar.
- `StartTimeZoneId` / `EndTimeZoneId`: Existing timezone identifiers.
- `SortOrder`: Existing ordering tie-breaker.
- `Items`: Events associated with the leg.

**Derived fields**:

- `EventCount`: `Items.Count`.
- `EventCountLabel`: `0 events`, `1 event`, or `# events`.
- `LegBandSpan`: Horizontal position and width computed from `StartLocal` and `EndLocal`.

**Validation rules**:

- `EventCount` must exactly match the number of `Items` rendered for the leg.
- The count must be visible in the sticky label column under each leg.
- The leg time range must remain visible in light and dark modes.
- Event bars must not cover the full row height; part of the lane must remain visible/clickable.

## Entity: Timeline Event Bar

**Purpose**: Selectable visual representation of an event associated with a trip leg.

**Existing fields used**:

- `TrackedItemId`: Stable event identity.
- `TripLegId`: Owning leg, if assigned.
- `Title`: Event label.
- `StartLocal` / `EndLocal`: Event span in the row.
- `StartsAt` / `EndsAt`: Existing instants used for ordering and range checks.
- `DisplayColor`: Event color token.
- `StartsOutsideLeg` / `EndsOutsideLeg`: Existing mismatch flags.
- `SortOrder`: Stable ordering tie-breaker.

**Derived presentation state**:

- `ItemSpan`: Horizontal position and width computed from event start/end values.
- `ItemBarHeight`: Approximately half the row height so it does not block all lane clicks.
- `ItemVerticalPlacement`: Placement that keeps the event readable and leaves a clickable row area.

**Validation rules**:

- Event bars remain directly selectable.
- Event bars do not intercept clicks across the full row height.
- Outside-leg events keep their existing warning treatment.

## Relationships

- A `Timeline Calendar` has many `Trip Leg Timeline Row` records.
- A `Trip Leg Timeline Row` has zero or more `Timeline Event Bar` records through `Items`.
- A new event created from a leg row is pre-associated with that row's `TripLegId`.

## State Transitions

1. **Add event from leg row**: traveler chooses an add-event action or available time within a leg row -> trip detail opens existing event form with `TripLegId` pre-selected -> save creates event -> timeline refreshes -> leg `EventCount` increments and event bar appears.
2. **Cancel add event**: traveler starts add-event flow -> cancels before save -> no new event is persisted -> timeline and count remain unchanged.
3. **Reassign/remove event**: existing event moves away from or is removed from a leg -> timeline refreshes -> affected legs recalculate `EventCount` from `Items.Count`.
4. **Theme switch**: traveler switches light/dark mode -> same leg/event data remains loaded -> CSS tokens update leg-band visibility without data changes.
