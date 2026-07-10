# Feature Specification: Timeline Event Entry Experience Enhancements

**Feature Branch**: `[018-timeline-event-entry-ux]`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "When I view the timeline and want to enter events, I have a few user interface ideas to smooth the user interaction. I want to track the active date viewed on the timeline to help date entry. I'd like an icon based on the event type to show on the timeline. I'd like the location field to be mappable."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prefill Event Dates from the Active Timeline Date (Priority: P1)

A traveler is viewing their trip timeline positioned on a particular day and decides to add an event. When the event entry opens, its start date is already set to the day the traveler was looking at, so they can confirm or adjust rather than typing the date from scratch.

**Why this priority**: Entering the date manually is the most repetitive and error-prone part of adding an event, especially on long trips. Because the traveler almost always adds an event for the day they are currently viewing, defaulting the date to the active timeline position removes the biggest source of friction and delivers immediate value on its own.

**Independent Test**: Can be fully tested by positioning the timeline on a specific day (not the trip's first day), starting a new event, and confirming the event's start date defaults to that day before any typing; then changing the timeline position and confirming a subsequent new event defaults to the new day.

**Acceptance Scenarios**:

1. **Given** the timeline is positioned on a specific day within the trip, **When** the traveler begins creating a new event, **Then** the event's start date defaults to the day the timeline is currently positioned on.
2. **Given** a new event has a start date defaulted from the active timeline day, **When** the traveler changes the start date before saving, **Then** the traveler's manual choice is kept and saved instead of the defaulted date.
3. **Given** the timeline has been moved to a different day since the last event was added, **When** the traveler creates another new event, **Then** the newly defaulted start date reflects the current timeline position, not the previous one.
4. **Given** the traveler edits an existing event rather than creating one, **When** the event entry opens, **Then** the event's own saved date is shown and is not overwritten by the active timeline day.

---

### User Story 2 - Recognize Events on the Timeline by Type Icon (Priority: P2)

A traveler scanning the timeline can tell at a glance what kind of item each entry is — an event, a reservation, an activity, or a reminder — because each one shows an icon that represents its type, without having to open the item to read its type.

**Why this priority**: Once events are quick to enter, travelers need to read the timeline efficiently. A type icon lets travelers distinguish reservations from activities or reminders at a glance, improving scannability, but it builds on entries already existing so it follows the entry improvement.

**Independent Test**: Can be fully tested by placing items of each supported type on the timeline and confirming each entry displays an icon that corresponds to its type, and that changing an item's type updates the icon shown.

**Acceptance Scenarios**:

1. **Given** the timeline contains items of different types, **When** the traveler views the timeline, **Then** each item shows an icon that corresponds to its type.
2. **Given** two items of the same type on the timeline, **When** the traveler views them, **Then** they show the same type icon so the type is visually consistent.
3. **Given** an item whose type is changed and saved, **When** the traveler returns to the timeline, **Then** the item's icon updates to reflect the new type.
4. **Given** an item whose type is missing or not one of the known types, **When** the traveler views the timeline, **Then** a clear default icon is shown so the entry is never left without a visual indicator.

---

### User Story 3 - Open an Event's Location on a Map (Priority: P3)

A traveler who has recorded a location on an event can act on that location directly from the event to see it on a map, so they can orient themselves or get directions without retyping the place into a separate map.

**Why this priority**: Making the location actionable is a convenience that helps travelers use the information they have already captured. It is valuable but layered on top of entry and readability improvements, so it comes last.

**Independent Test**: Can be fully tested by opening an event that has a location, invoking the map action, and confirming the traveler is taken to a map view for that location; then confirming an event with no location does not offer the map action.

**Acceptance Scenarios**:

1. **Given** an event that has a location recorded, **When** the traveler chooses the map action for that location, **Then** the traveler is taken to a map view centered on that location.
2. **Given** an event that has no location recorded, **When** the traveler views the event, **Then** no map action is offered for the location.
3. **Given** an event whose location text is present, **When** the traveler invokes the map action, **Then** the location text is used as entered to locate the place on the map.
4. **Given** the traveler invokes the map action, **When** the map opens, **Then** the traveler can return to the event without losing any unsaved work in progress.

### Edge Cases

- The trip timeline has no defined current position yet (for example, the timeline was just opened) when the traveler starts a new event — the event date must still default to a sensible day within the trip rather than an empty or out-of-range date.
- The active timeline day falls outside the range of the leg the event will belong to — the defaulted date is provided as a starting point and the traveler can still adjust it, and any existing out-of-range handling continues to apply.
- The traveler starts a new event, changes the timeline position while the entry is open, then saves — the date reflects what the traveler entered, not a later timeline movement.
- An item's type value is empty, unknown, or newly introduced — the timeline still shows a recognizable default icon.
- A location contains unusual characters, is very long, or is ambiguous — the map action still attempts to locate it using the text as entered without breaking the event view.
- A traveler on a small screen or using assistive technology needs the type icons and the map action to be perceivable and operable.
- A traveler views a trip or event they do not own — existing ownership restrictions continue to apply to these interactions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST track the day the trip timeline is currently positioned on so it can be used as context for entering events.
- **FR-002**: When a traveler begins creating a new event from the timeline, the system MUST default the event's start date to the day the timeline is currently positioned on.
- **FR-003**: The system MUST allow the traveler to change the defaulted start date, and MUST keep the traveler's manual choice instead of the default when the event is saved.
- **FR-004**: The system MUST reflect the current timeline position each time a new event is started, so the defaulted date follows the traveler's latest navigation rather than a stale position.
- **FR-005**: The system MUST NOT overwrite the saved date of an existing event with the active timeline day when the traveler opens that event to edit it.
- **FR-006**: When the timeline has no established current position, the system MUST default a new event's start date to a sensible day within the trip's date range rather than leaving it empty or outside the trip.
- **FR-007**: The system MUST display an icon on each timeline entry that corresponds to the entry's type (event, reservation, activity, or reminder).
- **FR-008**: The system MUST show the same icon for entries that share the same type so type indication is visually consistent.
- **FR-009**: The system MUST update an entry's displayed icon when the entry's type is changed and saved.
- **FR-010**: The system MUST show a clear default icon for any timeline entry whose type is missing or not one of the known types, so no entry is left without a visual indicator.
- **FR-011**: The system MUST provide an action to view an event's recorded location on a map when a location has been recorded for that event.
- **FR-012**: The system MUST NOT offer the map action for an event that has no location recorded.
- **FR-013**: The system MUST use the event's location text as entered when locating the place on the map.
- **FR-014**: The system MUST allow the traveler to return to the event after using the map action without losing unsaved work in progress on the event.
- **FR-015**: The type icons and the map action MUST be perceivable and operable across supported screen sizes and expose accessible labels so assistive technologies can identify their purpose.
- **FR-016**: These interactions MUST respect existing ownership restrictions so only a trip's owner can view and act on its timeline entries and event locations.

### Key Entities *(include if feature involves data)*

- **Timeline Position**: The day the trip timeline is currently focused on or scrolled to; used as the default start date when a new event is created.
- **Timeline Entry (Tracked Item)**: An event, reservation, activity, or reminder shown on the timeline; carries a type that determines the icon displayed and a location that may be acted upon.
- **Event Location**: The optional location text recorded on an event that, when present, can be opened on a map.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: When a traveler starts a new event from a specific timeline day, the event's start date matches that day before any manual typing in 100% of cases.
- **SC-002**: Travelers can add an event for the day they are viewing without manually entering the date in at least 90% of new-event flows.
- **SC-003**: Travelers can correctly identify a timeline entry's type from its icon alone (without opening it) in at least 90% of usability checks.
- **SC-004**: Every timeline entry displays a type-appropriate icon (or a clear default when the type is unknown) in 100% of cases, with no entry left without an icon.
- **SC-005**: Travelers with an event that has a location can reach a map view of that location in a single action in 100% of attempts.
- **SC-006**: The map action never appears for events without a recorded location in 100% of checks.

## Assumptions

- The trip timeline already tracks a current date position (from the existing timeline date navigation capability), and this feature reuses that position as the source of the default event date rather than introducing a new navigation concept.
- "Event" refers to the trip's tracked items (events, reservations, activities, and reminders); the type icon applies to all of these item types, and the date-default and location-map behaviors apply to items entered from the timeline.
- The set of item types is the existing set (event, reservation, activity, reminder); a distinct, recognizable icon is provided for each, plus a default icon for unknown or missing types.
- "Mappable location" means the recorded location text becomes an actionable shortcut that opens a map view of that place (for example, in a standard mapping experience); introducing a new stored geocoordinate field or an in-app interactive map with saved pins is out of scope for this version.
- The location is matched using the text the traveler already entered; this feature does not add geocoding accuracy guarantees.
- Existing light/dark theme and branding are reused for the new icons and the map action.
- Existing ownership and access rules for trips, legs, and events continue to apply unchanged.

> **Post-implementation addition (2026-07-10)**: An address typeahead was subsequently added to the location field — as the traveler types, the app suggests map-capable addresses from an address provider (Azure Maps) and lets them pick one. This was originally listed as out of scope ("place autocomplete"); it is now in scope. It remains free-text/optional (a suggestion is not required), the provider key stays server-side via a new authenticated API endpoint, and suggestions degrade to none when the provider is unconfigured or unavailable. See the plan for the technical approach.
