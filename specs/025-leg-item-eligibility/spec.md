# Feature Specification: Travel Leg Modes and Item Eligibility

**Feature Branch**: `main`

**Created**: 2026-08-06

**Status**: Draft

**Input**: User description: "Differentiate Travel and Stay legs. Travel legs capture a transportation mode (flight, train, bus, boat, or car), origin, destination, start/end, and time zones. Modes other than car capture travel cost and a reservation/confirmation number and do not permit items; car travel may contain items."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Describe a Travel or Stay Leg (Priority: P1)

A traveler creating a trip leg identifies it as either Travel or Stay. For Travel, they also select Flight, Train, Bus, Boat, or Car and record the route and schedule. Ticketed modes capture the booking reference and cost alongside the leg rather than as a separate item.

**Why this priority**: Item eligibility cannot be applied reliably until every leg has an explicit, durable classification.

**Independent Test**: Create one leg for each Travel mode and one Stay leg, reopen them, and verify that their classification, travel mode, route, schedule, time zones, and applicable booking details are retained.

**Acceptance Scenarios**:

1. **Given** a traveler is creating a leg, **When** they select Travel and save valid leg details, **Then** the leg is saved and subsequently identified as a Travel leg.
2. **Given** a traveler is creating a leg, **When** they select Stay and save valid leg details, **Then** the leg is saved and subsequently identified as a Stay leg.
3. **Given** an existing leg created before this feature, **When** the traveler first views or edits it after the change, **Then** it has a classification derived from its existing origin information without requiring traveler action.
4. **Given** a Travel leg, **When** the traveler enters its details, **Then** both an origin and destination are required.
5. **Given** a Stay leg, **When** the traveler enters its details, **Then** a destination is required and an origin is not requested.
6. **Given** a Travel leg, **When** the traveler saves it, **Then** exactly one mode from Flight, Train, Bus, Boat, or Car is required.
7. **Given** any Travel mode, **When** the traveler enters a travel cost or reservation or confirmation number, **Then** the supplied detail is saved with the leg.
8. **Given** any Travel mode, **When** the traveler omits travel cost and reservation or confirmation number, **Then** the leg can still be saved when its other details are valid.

---

### User Story 2 - Add Items Only Where Stops Are Controlled (Priority: P2)

A traveler can add itinerary items to a Stay or Car leg, where they control their schedule and stops. On a Flight, Train, Bus, or Boat leg, the product does not offer item creation and rejects any attempt to assign an item to that leg.

**Why this priority**: This is the core behavioral distinction requested and prevents itinerary entries from being placed inside travel periods where the traveler cannot independently schedule them.

**Independent Test**: Open Stay, Car, and ticketed Travel legs, verify that item creation is available for Stay and Car only, and confirm that direct assignment to every restricted mode is refused.

**Acceptance Scenarios**:

1. **Given** a Stay leg, **When** the traveler views the leg, **Then** they can start creating an item for that leg.
2. **Given** a Car leg, **When** the traveler views the leg, **Then** they can start creating an item for a stop during that journey.
3. **Given** a Flight, Train, Bus, or Boat leg, **When** the traveler views the leg, **Then** no action to add an item to that leg is offered.
4. **Given** a traveler attempts to create or assign an item to a restricted Travel mode through any supported workflow, **When** the request is validated, **Then** it is refused with an explanation that the selected mode cannot contain items.
5. **Given** an item whose timeframe fits both a restricted Travel leg and an eligible Stay or Car leg, **When** the system proposes eligible legs, **Then** only eligible legs are offered.
6. **Given** no eligible Stay or Car leg covers an item's timeframe, **When** the traveler saves the item, **Then** they may leave it unassigned under the existing unassigned-item rules.

---

### User Story 3 - Change a Leg Without Stranding Items (Priority: P3)

A traveler may correct a leg's classification or transportation mode. An eligible leg may become Car while retaining its items, but it cannot become Flight, Train, Bus, or Boat until its items have been moved or left unassigned. A Travel leg can become Stay when its resulting details are valid.

**Why this priority**: Classification mistakes must be correctable, but changing eligibility must not silently delete, hide, or invalidate existing itinerary data.

**Independent Test**: Change empty legs across classifications and modes, verify that a populated eligible leg can remain or become Car, then attempt to change it to Flight, Train, Bus, or Boat and verify that the change is blocked until its items are reassigned.

**Acceptance Scenarios**:

1. **Given** a Stay leg with no items, **When** the traveler changes it to a Travel mode and supplies all details required by that mode, **Then** the change succeeds.
2. **Given** a Travel leg, **When** the traveler changes it to Stay and supplies all required Stay details, **Then** the change succeeds.
3. **Given** an eligible leg with one or more items, **When** the traveler attempts to change it to Flight, Train, Bus, or Boat, **Then** the change is refused and the traveler is told to move or unassign the items first.
4. **Given** a refused classification change, **When** the traveler returns to the trip, **Then** the leg and all of its items retain their prior values and relationships.
5. **Given** a Car leg with items, **When** the traveler changes another travel detail but keeps Car as its mode, **Then** the change remains allowed subject to existing validation.

---

### User Story 4 - Preserve Existing Trips (Priority: P4)

A traveler opens a trip created before this distinction was persisted. Existing legs, items, dates, time zones, and relationships remain intact, and the traveler can continue planning without repairing migrated data.

**Why this priority**: The new rule must improve future planning without causing data loss or making established trips unusable.

**Independent Test**: Load representative pre-change trips containing legs with and without origins and with assigned items, then verify that all data remains visible and usable under the migration rules.

**Acceptance Scenarios**:

1. **Given** an existing leg with an origin, **When** existing data is classified, **Then** the leg becomes Travel with Car as its mode.
2. **Given** an existing leg without an origin, **When** existing data is classified, **Then** the leg becomes Stay.
3. **Given** existing items related to a leg classified as Travel with Car mode, **When** the trip is opened, **Then** those items remain visible and related and new items remain permitted under existing rules.

### Edge Cases

- An existing leg has an origin containing only whitespace; it is treated as having no origin and is classified as Stay.
- A traveler changes Travel to Stay; the no-longer-applicable origin is removed rather than retained as hidden data.
- A traveler changes Stay to Travel without supplying an origin; the change is refused and the original leg remains unchanged.
- An item is assigned through manual entry, email review, later reassignment, or another workflow; every path applies the same leg-eligibility rule.
- A traveler changes Car to a restricted Travel mode while items are assigned; the change is refused without changing the leg or its items.
- A leg's classification changes between opening an item form and saving it; eligibility is checked again at save time.
- A collaborator loses edit access or a target leg is deleted while an item is being assigned; existing authorization and stale-data protections still apply before eligibility is evaluated.
- Timeline and printable views display migrated items on Travel legs until the traveler chooses to reorganize them.
- A traveler enters a zero travel cost for a Travel mode; zero and an omitted cost are accepted, while a supplied negative cost is refused.
- A traveler changes a restricted Travel mode to Car; existing cost and confirmation details remain available unless the traveler clears them.

## Requirements *(mandatory)*

### Functional Requirements

**Leg classification**

- **FR-001**: Every trip leg MUST have exactly one explicit classification: Travel or Stay.
- **FR-002**: Users MUST select a classification when creating a leg and MUST be able to view that classification when viewing or editing the leg.
- **FR-003**: A Travel leg MUST require an origin and a destination.
- **FR-004**: A Stay leg MUST require a destination and MUST NOT retain an origin.
- **FR-005**: Both classifications MUST retain the existing leg title, start, end, start time zone, end time zone, and notes behavior.
- **FR-006**: Every Travel leg MUST have exactly one transportation mode: Flight, Train, Bus, Boat, or Car; a Stay leg MUST NOT have a transportation mode.
- **FR-007**: Every Travel mode MUST accept an optional travel cost and an optional reservation or confirmation number.
- **FR-008**: When supplied, travel cost MUST be nonnegative with no more than two decimal places, and reservation or confirmation number MUST be nonblank after trimming and no more than 255 characters.
- **FR-009**: The selected transportation mode and applicable travel details MUST be retained and displayed when the leg is viewed, edited, or printed.

**Item eligibility**

- **FR-010**: Stay and Car legs MUST permit item creation and assignment subject to the existing timeframe, trip, and permission rules.
- **FR-011**: Flight, Train, Bus, and Boat legs MUST NOT permit creation or assignment of items.
- **FR-012**: The product MUST omit or disable leg-specific item creation actions for restricted Travel modes and MUST explain why the action is unavailable where an explanation is needed.
- **FR-013**: The system MUST enforce mode-based item restrictions at the point data is saved, regardless of which traveler workflow initiated the assignment.
- **FR-014**: Leg suggestions and placement choices for an item MUST exclude restricted Travel modes.
- **FR-015**: An item that has no eligible Stay or Car leg MUST remain eligible for the existing unassigned-item workflow.
- **FR-016**: Existing timeframe containment and permission rules MUST continue to apply to items assigned to eligible legs.

**Classification changes**

- **FR-017**: Users MUST be able to change a leg classification or Travel mode when the resulting leg is valid.
- **FR-018**: The system MUST refuse changing an eligible leg to Flight, Train, Bus, or Boat while one or more items are assigned to it.
- **FR-019**: A refused classification or mode change MUST leave the leg and all related items unchanged.
- **FR-020**: When changing a Travel leg to Stay, the system MUST remove its origin, transportation mode, travel cost, and confirmation number after the traveler confirms and saves the change.
- **FR-021**: A classification or mode change MUST be revalidated when saved so a concurrent item assignment cannot create an invalid result.

**Existing data**

- **FR-022**: Existing legs with a nonblank origin MUST be classified as Travel with Car mode, and existing legs without a nonblank origin MUST be classified as Stay.
- **FR-023**: Migration MUST preserve all existing legs, items, dates, time zones, notes, and item-to-leg relationships.
- **FR-024**: Migrated Travel legs MUST continue to allow omitted travel cost and confirmation details after any classification or mode change.

### Key Entities

- **Trip Leg**: A dated segment of a trip with a title, classification, destination, travel window, time zones, and optional notes. A Travel leg also has an origin, transportation mode, and optional booking details.
- **Leg Classification**: The persisted meaning of a leg. Travel represents movement between places; Stay represents time at a destination.
- **Transportation Mode**: Flight, Train, Bus, Boat, or Car. It controls whether a Travel leg may contain items; every mode accepts the same optional booking details.
- **Item**: A planned event, reservation, activity, or reminder. It may be assigned only to an eligible Stay or Car leg whose travel window contains it, or remain unassigned under existing rules.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of newly created and edited legs retain their selected classification, transportation mode, and applicable travel details when reopened.
- **SC-002**: Across manual entry, email review, and item reassignment, 100% of new attempts to place an item on a Flight, Train, Bus, or Boat leg are refused before trip data changes.
- **SC-003**: In usability testing, at least 9 of 10 travelers correctly identify which legs can contain items before attempting item creation.
- **SC-004**: Travelers can create a valid Travel leg or Stay leg in the same number of form submissions required before this feature.
- **SC-005**: 100% of pre-change legs receive a classification and, for Travel legs, Car mode without traveler action; 100% of existing items and relationships remain available after migration.
- **SC-006**: A populated eligible leg cannot be changed to a restricted mode until all assigned items have been moved or unassigned, with zero silent item deletions or relationship changes.
- **SC-007**: Item creation is available on every otherwise-valid Stay and Car leg and unavailable on every Flight, Train, Bus, and Boat leg across timeline, review, and reassignment surfaces.
- **SC-008**: 100% of otherwise-valid Travel legs can be saved without cost or confirmation details, while 100% of supplied invalid cost or confirmation values are refused.

## Assumptions

- Travel and Stay are the complete set of leg classifications for this feature.
- Flight, Train, Bus, Boat, and Car are the complete transportation-mode set for this feature.
- Car is the only Travel mode that permits items because the traveler controls its stops.
- Existing origin data is the most reliable available signal for migrating legs because the current experience already infers Travel versus Stay from whether origin is populated.
- Existing Travel legs migrate to Car because that is the only mode compatible with preserving any assigned items without a legacy exception.
- The existing unassigned-item behavior remains available and is not redesigned by this feature.
- Existing trip access controls, item timeframe validation, notifications, and deletion behavior remain unchanged except where item eligibility explicitly applies.
- Travel cost uses the application's existing trip currency display convention; multi-currency conversion remains out of scope.

## Dependencies

- Existing trip-leg creation and editing experience.
- Existing item creation, email review, reassignment, unassigned-item, timeline, and printable-trip workflows.
- Existing trip permissions and item-within-leg timeframe validation.

## Out of Scope

- Capturing carrier, flight number, train service, bus route, vessel, vehicle, seat, terminal, or platform details.
- Automatically moving or deleting existing items from legs classified as Travel.
- Creating Stay legs automatically when an item cannot be assigned.
- Changing the existing item types or the meaning of unassigned items.