# Phase 0 Research: Trip Leg and Event Timeline Adjustments

## Decision: Derive event counts from `TimelineLeg.Items.Count`

**Rationale**: The timeline response already groups each event under its owning `TimelineLeg`. Displaying the count from the existing in-memory collection avoids duplicate count state, avoids new SQL aggregation, and guarantees the UI count changes when the timeline item list changes after refresh.

**Alternatives considered**:

- Add `EventCount` to `TimelineLeg`: rejected for now because it duplicates `Items.Count` and creates a risk of stale or inconsistent counts unless there is a future need to omit item details from the response.
- Add a separate count endpoint: rejected because it would add latency and extra API surface for data the timeline already has.

## Decision: Display counts in the sticky trip-leg label as `# events`

**Rationale**: Trip legs are shown in the left sticky label column, which remains visible while the traveler scrolls horizontally through the calendar. Placing the count below each leg keeps the count attached to the row identity and satisfies the requirement that each leg include `# events` below it.

**Alternatives considered**:

- Place counts inside the time grid: rejected because counts would scroll away horizontally and compete with time-range/event bars.
- Show counts only in tooltips: rejected because travelers need to scan counts without hovering or opening details.

## Decision: Keep add-event behavior in the existing timeline/trip detail callback flow

**Rationale**: `TripTimeline.razor` already emits `TimelineSlotSelection` through `OnLegSlotSelected`, and `TripDetails.razor` already owns the event creation modal flow. Extending that path keeps the timeline component focused on selection/presentation and preserves existing event validation/API behavior.

**Alternatives considered**:

- Create a new timeline-specific add-event endpoint: rejected because the tracked item create endpoint already supports creating events associated with a trip leg.
- Let the timeline component own the full event modal: rejected because it would duplicate trip detail form behavior and make state coordination harder.

## Decision: Make event bars approximately half-row height and reserve a lane click target

**Rationale**: The current `.ttl-item` height nearly fills `--ttl-row-h`, which means a long event can block the traveler from clicking a time within that leg. Event bars should be vertically constrained (roughly half the row height) and positioned so part of the leg row remains visible/clickable. This preserves direct event selection while restoring access to the underlying time range.

**Alternatives considered**:

- Reduce event opacity but keep full height: rejected because transparent buttons still intercept pointer events.
- Put event bars behind the lane: rejected because events would become harder to select and read.
- Disable event pointer events and select events another way: rejected because it would regress direct event selection from the timeline.

## Decision: Improve dark-mode leg-band contrast with timeline-specific tokens

**Rationale**: The leg time range is currently shown as `.ttl-leg-band` using the general brand-soft token at partial opacity. In dark mode that can become too subtle against the raised timeline surface. A dedicated band background/border/shadow treatment can make the range easy to spot while preserving the existing theme system.

**Alternatives considered**:

- Use a single hard-coded color: rejected because the application already has light/dark design tokens.
- Increase opacity globally: rejected because light mode may become visually heavy and obscure events/grid lines.
