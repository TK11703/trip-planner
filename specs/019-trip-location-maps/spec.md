# Feature Specification: Trip Location Maps

**Feature Branch**: `[019-trip-location-maps]`

**Created**: 2026-07-10

**Status**: Draft

**Input**: User description: "Location determinations - I'd like the user to be able to choose their map output, but I'd also like to create a built in map to show their trips locations."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See the Whole Trip on a Built-In Map (Priority: P1)

A traveler wants a geographic overview of where their trip takes them. They open a built-in map for the trip and see every event that has a recorded location plotted as a point on the map, letting them understand the shape and spread of their journey at a glance without leaving the app.

**Why this priority**: A single, in-app view of all trip locations is the most valuable new capability in this feature and stands on its own. Even without any preference settings or per-point interactions, being able to visualize the entire trip geographically delivers immediate, complete value and is the natural minimum viable slice.

**Independent Test**: Can be fully tested by opening a trip that has several events with recorded locations, invoking the trip's built-in map, and confirming each locatable event appears as a point and the map frames all of them; then confirming a trip with no locatable events shows a clear empty state rather than a broken or blank map.

**Acceptance Scenarios**:

1. **Given** a trip with multiple events that have recorded locations, **When** the traveler opens the trip's built-in map, **Then** each locatable event is shown as a point on the map.
2. **Given** a trip whose locatable events are spread across a wide area, **When** the built-in map opens, **Then** the map is centered and framed so all plotted locations are visible at once.
3. **Given** a trip that has events without any recorded location, **When** the built-in map opens, **Then** those events are omitted from the map and only located events are plotted.
4. **Given** a trip that has no events with recorded locations, **When** the traveler opens the built-in map, **Then** a clear empty state is shown that explains there are no locations to display.
5. **Given** an event whose recorded location text cannot be resolved to a place, **When** the built-in map opens, **Then** the map still displays the resolvable locations and does not fail because of the unresolved one.

---

### User Story 2 - Choose Which Map Opens a Location (Priority: P2)

A traveler has a preferred way to view maps — an external mapping service they already use, or the trip's built-in map. They set their preferred map output once, and from then on, whenever they act on an individual event's location, it opens in the map they chose.

**Why this priority**: Choosing the map output personalizes how travelers act on a single location and builds on the location map action they already have. It is valuable customization but layered on top of the core built-in map, so it follows the primary visualization capability.

**Independent Test**: Can be fully tested by setting a map output preference, invoking the map action on an event that has a location, and confirming the location opens in the chosen map; then changing the preference and confirming a subsequent map action honors the new choice.

**Acceptance Scenarios**:

1. **Given** the traveler has chosen a preferred map output, **When** they invoke the map action on an event's location, **Then** the location opens in the map output they selected.
2. **Given** the traveler changes their preferred map output, **When** they next invoke the map action on a location, **Then** the location opens in the newly chosen map output rather than the previous one.
3. **Given** the traveler has not chosen a preferred map output, **When** they invoke the map action on a location, **Then** the location opens in a sensible default map output.
4. **Given** the built-in map is offered as one of the map output choices, **When** the traveler selects it and invokes the map action, **Then** the location opens in the trip's built-in map focused on that place.
5. **Given** the traveler's chosen map output is unavailable, **When** they invoke the map action, **Then** the system falls back gracefully to an available map output instead of failing silently.

---

### User Story 3 - Identify and Open an Event from a Map Point (Priority: P3)

A traveler looking at the built-in trip map can tell which event a plotted point represents and move from that point directly to the event's details, so the map becomes a way to navigate the trip rather than just a static picture.

**Why this priority**: Making plotted points identifiable and actionable turns the built-in map into a navigation aid. It is a meaningful enhancement but depends on the built-in map existing, so it comes last.

**Independent Test**: Can be fully tested by opening the built-in map for a trip with several located events, selecting a plotted point, and confirming the point identifies its event and offers a way to open that event's details.

**Acceptance Scenarios**:

1. **Given** the built-in map shows several plotted locations, **When** the traveler selects one, **Then** the point identifies which event it represents.
2. **Given** a plotted point identifies its event, **When** the traveler chooses to view that event, **Then** they are taken to the event's details.
3. **Given** two events at the same or very close locations, **When** the traveler views the map, **Then** they can still distinguish and select each event rather than only reaching one.

### Edge Cases

- A trip has a single locatable event — the built-in map still frames that one point sensibly rather than zooming to an extreme or unusable level.
- An event's location text is ambiguous, unusually formatted, or very long — the map resolves what it can using the text as entered and omits or flags points it cannot place, without breaking the view.
- The location-resolving provider is unconfigured, rate-limited, or unavailable — the built-in map degrades to an empty or partial state with a clear message rather than an error.
- A traveler's preferred external map output is not installed or reachable on their device — the system falls back to an available option.
- Many events share nearly identical coordinates — the traveler can still perceive and select each one.
- A traveler on a small screen or using assistive technology needs the map, its points, and the preference control to be perceivable and operable.
- A traveler views a trip they do not own — existing ownership restrictions continue to apply to the built-in map and to map-output actions.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a built-in, in-app map view for a trip that plots the trip's events which have a recorded location.
- **FR-002**: The system MUST show each locatable event as a distinct point on the built-in map.
- **FR-003**: The system MUST center and frame the built-in map so that all plotted locations for the trip are visible when the map opens.
- **FR-004**: The system MUST omit events that have no recorded location from the built-in map, plotting only events that have a location.
- **FR-005**: The system MUST show a clear empty state on the built-in map when the trip has no events with recorded locations.
- **FR-006**: The system MUST derive each point's position from the event's recorded location text, and MUST continue to display the resolvable locations when one or more location texts cannot be resolved to a place.
- **FR-007**: The system MUST allow a traveler to choose a preferred map output from the available options, including at least one external mapping option and the trip's built-in map.
- **FR-008**: The system MUST persist the traveler's chosen map output preference so it applies to future map actions without being re-selected each time.
- **FR-009**: When a traveler invokes the map action on an individual event's location, the system MUST open that location using the traveler's preferred map output.
- **FR-010**: The system MUST apply a sensible default map output when the traveler has not chosen a preference.
- **FR-011**: The system MUST fall back to an available map output when the traveler's chosen map output is unavailable, rather than failing silently.
- **FR-012**: When the built-in map is the selected map output for an individual location, the system MUST open the built-in map focused on that location.
- **FR-013**: The system MUST allow the traveler to select a plotted point on the built-in map and identify which event it represents.
- **FR-014**: The system MUST allow the traveler to navigate from a plotted point to the corresponding event's details.
- **FR-015**: The system MUST allow the traveler to distinguish and select events that share the same or very close locations on the built-in map.
- **FR-016**: The built-in map, its plotted points, and the map-output preference control MUST be perceivable and operable across supported screen sizes and expose accessible labels so assistive technologies can identify their purpose.
- **FR-017**: The built-in map and map-output actions MUST respect existing ownership restrictions so only a trip's owner can view and act on its locations.

### Key Entities *(include if feature involves data)*

- **Trip Map**: The aggregate in-app view for a trip that plots the recorded locations of the trip's events so the traveler can see the whole journey geographically.
- **Plotted Location**: A point on the trip map derived from an event's recorded location text; it references the event it represents so the traveler can identify and open that event.
- **Map Output Preference**: The traveler's chosen way to view a location when acting on it — an external mapping option or the built-in map — persisted and applied to future map actions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: When a traveler opens the built-in map for a trip with located events, every event that has a resolvable location appears as a point in 100% of cases.
- **SC-002**: When the built-in map opens, all plotted locations are visible within the initial view without the traveler needing to manually zoom or pan in at least 95% of cases.
- **SC-003**: A traveler can understand the geographic spread of their trip from the built-in map without opening individual events in at least 90% of usability checks.
- **SC-004**: A traveler's chosen map output is used for individual location map actions in 100% of cases after they set the preference, until they change it.
- **SC-005**: A traveler can set or change their preferred map output in a single, self-evident step in at least 90% of usability checks.
- **SC-006**: The built-in map never fails to load because of a single unresolved or invalid location; it displays the remaining resolvable locations in 100% of such cases.
- **SC-007**: A traveler can move from a plotted point on the built-in map to the corresponding event's details in a single action in 100% of attempts.

## Assumptions

- This feature builds on the existing event location map action (from the event detail shortcuts / timeline event entry work): opening a single location on a map already exists, and this feature governs which map that action uses and adds an aggregate trip map.
- "Choose their map output" means a per-user preference for how an individual location opens — either an external mapping service the traveler prefers or the trip's built-in map — rather than a per-trip or per-event setting.
- The set of external map output options is a small, common set (for example, a general web/consumer mapping option) plus the built-in map; the exact provider list is an implementation detail to be settled in planning, and a sensible default applies when no preference is set.
- The built-in map plots locations by resolving the event's recorded location text to coordinates using the existing address/location provider already integrated for location suggestions (Azure Maps); no new stored geocoordinate field on events is assumed, and locations that cannot be resolved are simply omitted from the map.
- "Trips locations" refers to the recorded locations on the trip's events (events, reservations, activities, reminders); locations are optional, so trips may have few, many, or no plottable points.
- Existing light/dark theme and branding are reused for the built-in map, its points, and the preference control.
- Existing ownership and access rules for trips, legs, and events continue to apply unchanged to the built-in map and map-output actions.
- Provider credentials for any external or map-tile services remain server-side and are not exposed to the browser, consistent with the existing location provider integration.
