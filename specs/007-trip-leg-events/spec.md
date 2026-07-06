# Feature Specification: Trip Leg Events and Timeline

**Feature Branch**: `[007-trip-leg-events]`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "The user initially knows their timeframe for the trip (start/end dates). However the trip legs may vary. The user is going to be interested in a timeline aspect to their trip. And an event needs to be related to a trip leg."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Relate an Event to a Trip Leg (Priority: P1)

A traveler adds an event to their trip and assigns it to a specific trip leg so the event is anchored to the correct segment of the journey (for example, a dinner reservation attached to the "Paris" leg rather than the trip as a whole).

**Why this priority**: Relating events to a trip leg is the foundational capability of this feature. Without it, events float at the trip level and cannot be organized against the parts of the journey they actually belong to. Every other capability depends on this relationship existing.

**Independent Test**: Can be fully tested by opening a trip that has at least one leg, creating an event, selecting the leg it belongs to, saving, and confirming the event is shown as belonging to that leg after leaving and returning to the trip.

**Acceptance Scenarios**:

1. **Given** a trip has at least one trip leg, **When** the traveler creates an event and selects a trip leg for it, **Then** the event is saved and shown as belonging to the selected leg.
2. **Given** an existing event that belongs to one trip leg, **When** the traveler reassigns it to a different trip leg on the same trip, **Then** the event is shown under the newly selected leg and no longer under the previous leg.
3. **Given** the traveler is creating an event, **When** they attempt to save it without selecting a trip leg, **Then** the system prevents the save and asks them to choose a trip leg.
4. **Given** a trip has no trip legs yet, **When** the traveler tries to add an event, **Then** the system explains that a trip leg must exist first and guides them to create one.

---

### User Story 2 - View a Trip Timeline Organized by Legs (Priority: P2)

A traveler views a chronological timeline of their trip that presents each trip leg in order along with the events that belong to each leg, so they can see how the journey and its events unfold over time.

**Why this priority**: The timeline is the primary way the traveler consumes the leg-and-event relationships created in User Story 1. It turns individually related events into an at-a-glance view of the whole trip, but it is only meaningful once events can be related to legs.

**Independent Test**: Can be fully tested by opening a trip that has multiple legs and several events across those legs, viewing the timeline, and confirming legs appear in chronological order with each event listed under the leg it belongs to.

**Acceptance Scenarios**:

1. **Given** a trip has multiple trip legs with events, **When** the traveler opens the trip timeline, **Then** the legs are presented in chronological order and each event appears under the leg it belongs to.
2. **Given** a trip leg has multiple events, **When** the traveler views that leg on the timeline, **Then** the events are ordered chronologically within the leg.
3. **Given** trip legs vary from the trip's initially entered start and end dates, **When** the traveler views the timeline, **Then** the timeline spans the full range covered by the trip dates and its legs so no leg or event is hidden.
4. **Given** a trip leg has no events, **When** the traveler views the timeline, **Then** the leg is still shown with an indication that it has no events yet.

---

### User Story 3 - Keep Events Related as Legs Change (Priority: P3)

A traveler edits, reorders, or removes trip legs after events already exist, and the system keeps events correctly related to legs, helps resolve events that no longer have a valid leg, and keeps the timeline accurate.

**Why this priority**: Trips evolve after initial planning; legs get re-dated, reordered, or deleted. Travelers need confidence that changing legs will not silently lose events or leave the timeline in a confusing state, but this only matters after relating and viewing are in place.

**Independent Test**: Can be fully tested by changing a leg's dates, reordering legs, and deleting a leg that has events on an existing trip, then confirming the events remain accounted for and the timeline reflects the changes.

**Acceptance Scenarios**:

1. **Given** a trip leg has events and the traveler changes that leg's start or end timing, **When** the change is saved, **Then** the events remain related to the same leg and the timeline reflects the leg's new timing.
2. **Given** the traveler reorders trip legs, **When** the reorder is saved, **Then** each event stays related to its own leg and the timeline reflects the new leg order.
3. **Given** a trip leg has events and the traveler attempts to delete that leg, **When** they confirm the deletion, **Then** the system requires the events to be reassigned to another leg or removed before the leg is deleted, so no event is left without a leg.
4. **Given** an event exists that is not related to any trip leg (for example, from before this feature), **When** the traveler views the trip, **Then** the event is shown as unassigned and the traveler is prompted to relate it to a trip leg.
5. **Given** an event's timing falls outside the timeframe of the leg it belongs to, **When** the traveler saves it, **Then** the system allows the save but flags the event so the traveler can review the mismatch.

### Edge Cases

- A trip has no trip legs when the traveler tries to add or view events.
- A trip leg is deleted while it still has events related to it.
- An event's start or end time falls outside its trip leg's start/end timeframe.
- Trip legs extend earlier than the trip's initial start date or later than its initial end date.
- Multiple trip legs overlap in time, and an event could reasonably belong to more than one.
- Events created before this feature have no related trip leg.
- A traveler reassigns an event to a leg on a different trip (should not be allowed).
- A traveler attempts to view or modify events or legs on a trip they do not own.
- A trip leg has no events and must still be represented on the timeline.
- Two events within the same leg share the exact same start time and need a stable ordering.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a traveler to relate each event to exactly one trip leg within the same trip.
- **FR-002**: The system MUST require a trip leg to be selected before an event can be saved.
- **FR-003**: The system MUST prevent creating an event on a trip that has no trip legs and MUST guide the traveler to create a trip leg first.
- **FR-004**: The system MUST allow a traveler to reassign an existing event from one trip leg to another trip leg within the same trip.
- **FR-005**: The system MUST prevent relating an event to a trip leg that belongs to a different trip.
- **FR-006**: The system MUST present a per-trip timeline that lists trip legs in chronological order.
- **FR-007**: The system MUST show each event under the trip leg it is related to on the timeline.
- **FR-008**: The system MUST order events chronologically within each trip leg on the timeline.
- **FR-009**: The system MUST span the timeline across the full range covered by the trip's dates and all of its trip legs, even when legs vary from the trip's initially entered start and end dates.
- **FR-010**: The system MUST display a trip leg on the timeline even when the leg has no related events, with a clear indication that it has none.
- **FR-011**: The system MUST keep events related to their trip leg when the leg's timing is changed or the legs are reordered.
- **FR-012**: The system MUST require the traveler to reassign or remove a leg's related events before that trip leg can be deleted.
- **FR-013**: The system MUST identify events that are not related to any trip leg as unassigned and prompt the traveler to relate them to a trip leg.
- **FR-014**: The system MUST allow saving an event whose timing falls outside its trip leg's timeframe while flagging the mismatch for the traveler to review.
- **FR-015**: The system MUST restrict viewing and modifying trip legs, events, and the timeline to the trip's owner.
- **FR-016**: The system MUST provide a stable, predictable ordering for events that share the same start time within a trip leg.

### Key Entities *(include if feature involves data)*

- **Trip**: The overall journey with an initially entered start date and end date. Owns trip legs and events.
- **Trip Leg**: A dated segment of the trip (with its own start and end timing) that events are related to. A trip has one or more legs; legs may vary from the trip's initial timeframe.
- **Event**: A trip activity, reservation, reminder, or scheduled occurrence that MUST be related to exactly one trip leg within its trip and has its own timing.
- **Timeline**: A per-trip chronological presentation that organizes trip legs in order and the events related to each leg.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A traveler can relate a new event to a trip leg in under 30 seconds without external guidance.
- **SC-002**: 100% of events shown on the timeline appear under the trip leg they are related to.
- **SC-003**: When viewing a trip with multiple legs and events, the timeline presents legs in correct chronological order and events in correct chronological order within each leg every time.
- **SC-004**: No event is ever silently left without a trip leg after a leg is deleted; every such event is either reassigned or explicitly removed by the traveler.
- **SC-005**: 95% of travelers can locate a specific event on the timeline by first finding the leg it belongs to on their first attempt.
- **SC-006**: Travelers can identify and resolve every unassigned event (including those created before this feature) through a clear prompt to relate it to a trip leg.

## Assumptions

- Trips already support an initially entered start date and end date, and this feature does not change how that timeframe is first captured.
- Trip legs already exist as dated segments of a trip; this feature relates events to those legs rather than redefining legs.
- "Event" refers to the trip's tracked occurrences (such as activities, reservations, and reminders); every such occurrence is treated as relatable to a trip leg.
- Each event relates to exactly one trip leg; splitting a single event across multiple legs is out of scope for this version.
- Trip legs may extend beyond or fall short of the trip's initially entered start and end dates, and the timeline accommodates that variation rather than forcing legs to fit the original dates.
- Events created before this feature may have no related trip leg and are treated as unassigned until the traveler relates them.
- Ownership and access control follow the existing model in which a trip and its contents are private to the trip's owner.
- Timezone handling for legs and events follows the behavior already established for trip legs and is not redefined here.
