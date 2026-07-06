# Research: Trip Leg Events and Timeline

## Decision: Build a first-party trip resource timeline instead of using FullCalendar resource timeline

**Rationale**: The desired resource timeline interaction maps naturally to FullCalendar's premium resource timeline, but that feature requires a paid subscription. Building the trip-specific timeline keeps the application free of that premium dependency while allowing the UI to match the product model exactly: trip legs on the left and a horizontally scrollable time grid on the right.

**Alternatives considered**:

- Use FullCalendar premium resource timeline: rejected because it introduces a paid subscription requirement.
- Continue using standard FullCalendar time grid: rejected because it cannot present legs as first-class resource rows without the premium resource timeline plugin.
- Use a generic HTML table: rejected because synchronized horizontal and vertical scrolling, sticky headers, fixed 30-minute slots, and overlayed event blocks need more control than a table provides cleanly.

## Decision: Model trip legs as timeline resources and tracked items as resource events

**Rationale**: The user wants the left column to list trip legs and the right grid to display each leg's events in that row. This means the timeline projection should be shaped around `TripLeg` rows that contain their related events, rather than returning one flat list of calendar events.

**Alternatives considered**:

- Keep the existing flat `TimelineEvent` projection and group on the client: rejected because the client would need to infer resource rows, unassigned items, and leg/item relationships from insufficient contract data.
- Add a separate resource API only for the UI: rejected because the trip timeline is a core trip projection and should remain available through the existing timeline boundary.

## Decision: Persist `TripLegId` on tracked items and validate same-trip ownership

**Rationale**: Events must belong to a trip leg. Persisting the relationship in `tracked_items` gives the API, database, timeline, and trip detail views one authoritative relationship. Validation must confirm the selected leg belongs to the same trip and owner to prevent cross-trip assignment.

**Alternatives considered**:

- Infer the leg from the event start time: rejected because overlapping legs, ambiguous travel days, and events outside leg ranges need explicit user intent.
- Allow multiple legs per event: rejected by the spec assumption that each event belongs to exactly one leg for this version.

## Decision: Persist selectable event color on tracked items

**Rationale**: The timeline needs each trip event to include a selectable display color. Persisting the color with the tracked item keeps the choice consistent across detail views, timeline reloads, API responses, and future clients.

**Alternatives considered**:

- Derive color from item type: rejected because the user specifically requested selectable color.
- Store color only in browser state: rejected because the color must survive reloads and be visible across sessions/devices.

## Decision: Launch existing modals on timeline selection instead of keeping a selected-item details pane

**Rationale**: The trip detail page already has modal forms for creating/editing legs and tracked items. Reusing those modals preserves existing edit flows while removing the side pane that the user explicitly does not want for this timeline.

**Alternatives considered**:

- Keep the existing side pane and add modal buttons: rejected because it conflicts with the requested interaction model.
- Build a separate timeline-specific editor: rejected because it duplicates existing item and leg forms.

## Decision: Allow legacy unassigned items during migration, but require a leg for all new and updated items

**Rationale**: Existing tracked items may not have a leg relationship. The migration should not invent incorrect relationships silently. New and updated items should require `TripLegId`, while the timeline can expose legacy unassigned items for resolution.

**Alternatives considered**:

- Auto-assign existing items to the nearest leg: rejected because overlapping legs and out-of-range items can make the automatic choice wrong.
- Make `TripLegId` immediately non-null: rejected because existing data could fail migration without a reliable assignment path.

## Decision: Use fixed 30-minute grid slots with sticky leg labels and headers

**Rationale**: Fixed slot widths make event block position and duration deterministic, keep scrolling predictable, and match the requested two columns per hour. Sticky headers and a sticky left leg column preserve context while scrolling across long trip ranges.

**Alternatives considered**:

- Variable-width time columns: rejected because it would make alignment and event placement less predictable.
- Day-only blocks: rejected because the requested timeline needs an hour row and 30-minute granularity.