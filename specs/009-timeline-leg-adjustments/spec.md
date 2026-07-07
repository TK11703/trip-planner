# Feature Specification: Trip Leg and Event Timeline Adjustments

**Feature Branch**: `[009-timeline-leg-adjustments]`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "trip leg/event adjustments - The trip timeline is going to be the primary focus here. We need the trip leg time ranges on the calendar to be easier to spot in dark mode. We need to know how many events are per trip leg from the time line calendar. We need to be able to easily add more events per trip leg from the trip legs row."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add Events to a Trip Leg from Its Timeline Row (Priority: P1)

A traveler viewing the trip timeline adds a new event to a specific trip leg directly from that leg's row on the calendar, without leaving the timeline or navigating elsewhere first, so building out a leg's plans is quick and stays in context.

**Why this priority**: Adding events per leg is the most impactful workflow change requested. The timeline is the primary place travelers work with their trip, and letting them add events right from the leg they are looking at removes the biggest source of friction. It delivers standalone value even if the other adjustments are not yet in place.

**Independent Test**: Can be fully tested by opening a trip timeline with at least one leg, using the add-event action on a specific leg's row, creating the event, and confirming the new event is saved and appears under that leg on the timeline.

**Acceptance Scenarios**:

1. **Given** a trip timeline showing at least one trip leg, **When** the traveler uses the add-event action on a specific leg's row, **Then** they can create a new event that is pre-associated with that leg.
2. **Given** the traveler adds an event from a leg's row, **When** the event is saved, **Then** it appears under that same leg on the timeline without a full reload of the trip.
3. **Given** the traveler starts adding an event from a leg's row, **When** they cancel before saving, **Then** no event is created and the timeline is unchanged.
4. **Given** a trip leg already has one or more events, **When** the traveler adds another event from that leg's row, **Then** the new event is added alongside the existing events under the same leg.

---

### User Story 2 - See Event Counts per Trip Leg on the Timeline (Priority: P2)

A traveler scanning the trip timeline sees, for each trip leg, how many events are associated with it, so they can tell at a glance which legs are full, which are sparse, and where more planning is needed.

**Why this priority**: Knowing the number of events per leg turns the timeline into an at-a-glance planning aid and helps travelers decide where to add events (User Story 1). It adds clear value on its own but is a read-only enhancement, so it ranks below the ability to add events.

**Independent Test**: Can be fully tested by opening a trip timeline whose legs have differing numbers of events and confirming each leg displays a count that matches the actual number of events associated with it.

**Acceptance Scenarios**:

1. **Given** a trip leg with several associated events, **When** the traveler views the timeline, **Then** the leg shows a count equal to the number of events associated with it.
2. **Given** a trip leg with no associated events, **When** the traveler views the timeline, **Then** the leg shows a count of zero (or an equivalent clear "no events" indication).
3. **Given** the traveler adds or removes an event on a leg, **When** the change is saved, **Then** that leg's event count updates to reflect the new total.
4. **Given** a trip with multiple legs, **When** the traveler views the timeline, **Then** every leg displays its own independent event count.

---

### User Story 3 - Distinguish Trip Leg Time Ranges in Dark Mode (Priority: P3)

A traveler using the application in dark mode can clearly spot each trip leg's time range on the calendar, so legs remain easy to locate and differentiate without straining to see them against a dark background.

**Why this priority**: Readability of leg time ranges in dark mode improves the experience for travelers who prefer dark mode, but the timeline is still usable in light mode and with the other adjustments, so it is the lowest of the three priorities while still being a requested outcome.

**Independent Test**: Can be fully tested by switching the application to dark mode, opening a trip timeline with multiple legs, and confirming each leg's time range is clearly visible and distinguishable from adjacent legs and from the calendar background.

**Acceptance Scenarios**:

1. **Given** the application is in dark mode, **When** the traveler views a trip timeline, **Then** each trip leg's time range is clearly visible against the calendar background.
2. **Given** the application is in dark mode and a trip has adjacent or overlapping legs, **When** the traveler views the timeline, **Then** each leg's time range remains distinguishable from the others.
3. **Given** the traveler switches between light mode and dark mode, **When** the timeline is shown, **Then** leg time ranges stay clearly readable in both modes.

### Edge Cases

- A traveler tries to add an event from a leg's row on a trip they do not own.
- A trip has no trip legs, so there are no leg rows to add events from or count events for.
- A trip leg has a very large number of events and its count must still be shown clearly.
- The add-event action is used on a leg's row but the trip leg is deleted or changed before the event is saved.
- A trip has many overlapping legs that must all remain visually distinguishable in dark mode.
- Event counts must stay correct after an event is reassigned from one leg to another.
- The timeline is viewed on a narrow or small screen where leg rows and counts still need to be legible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide an add-event action on each trip leg's row within the trip timeline.
- **FR-002**: The system MUST pre-associate an event created from a leg's row with that specific trip leg.
- **FR-003**: The system MUST show a newly added event under its leg on the timeline without requiring a full reload of the trip.
- **FR-004**: The system MUST allow the traveler to cancel adding an event from a leg's row without creating an event or otherwise changing the timeline.
- **FR-005**: The system MUST allow multiple events to be added to the same trip leg over time from its row.
- **FR-006**: The system MUST display, for each trip leg on the timeline, a count of the events associated with that leg.
- **FR-007**: The system MUST show a clear zero or "no events" indication for a trip leg that has no associated events.
- **FR-008**: The system MUST update a trip leg's displayed event count when events are added to, removed from, or reassigned away from that leg.
- **FR-009**: The system MUST display each trip leg's event count independently from the counts of other legs.
- **FR-010**: The system MUST make each trip leg's time range clearly visible on the calendar in dark mode.
- **FR-011**: The system MUST keep adjacent or overlapping trip leg time ranges distinguishable from one another in dark mode.
- **FR-012**: The system MUST keep trip leg time ranges clearly readable in both light mode and dark mode.
- **FR-013**: The system MUST restrict adding events from a leg's row to the trip's owner.
- **FR-014**: The system MUST meet applicable readability/contrast expectations for leg time ranges so they are perceivable in dark mode.

### Key Entities *(include if feature involves data)*

- **Trip Leg**: A dated segment of a trip shown as a row on the timeline; it has a time range and a set of associated events, and it is the target for adding new events and for the displayed event count.
- **Event**: A trip activity, reservation, reminder, or scheduled occurrence associated with exactly one trip leg; events created from a leg's row are pre-associated with that leg and contribute to its count.
- **Timeline Calendar**: The per-trip chronological presentation that shows trip legs as time ranges, exposes the per-leg add-event action, and displays each leg's event count.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A traveler can add a new event to a specific trip leg from its timeline row in under 20 seconds without navigating away from the timeline.
- **SC-002**: 100% of trip legs on the timeline display an event count that matches the actual number of events associated with them.
- **SC-003**: A trip leg's displayed event count reflects an added, removed, or reassigned event immediately after the change is saved, every time.
- **SC-004**: In dark mode, travelers can correctly identify and differentiate each leg's time range on a trip with multiple legs on their first attempt.
- **SC-005**: Leg time ranges remain clearly readable when switching between light and dark mode, with no mode requiring the traveler to hover or zoom to locate a leg.
- **SC-006**: Travelers can determine which legs need more events by reading per-leg counts on the timeline without opening any leg's details.

## Assumptions

- A per-trip timeline that presents trip legs and their associated events already exists (from the prior trip leg events and timeline feature), and this feature adjusts and enhances that timeline rather than creating it.
- Trip legs already exist as dated segments with a start and end, and events are already associated with exactly one trip leg.
- The application already supports both light mode and dark mode, and this feature improves leg time-range visibility within the existing dark mode rather than introducing theming.
- "Event count" means the total number of events currently associated with a leg, with no distinction by event type or status for this version.
- Adding an event from a leg's row uses the trip's existing event creation behavior and validation, with the leg pre-selected.
- Only the trip owner can view and modify a trip's timeline, legs, and events, consistent with existing ownership rules.
