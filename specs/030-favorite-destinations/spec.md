# Feature Specification: Favorite Destinations

**Feature Branch**: `main`

**Created**: 2026-09-28

**Status**: Draft

**Input**: Users want to maintain reusable favorite destinations, find them quickly, import existing lists, and select a favorite while creating a trip item.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Save a Researched Destination (Priority: P1)

A signed-in traveler saves a place they may want to visit in the future without first creating or choosing a trip. They enter a place name and address; the system derives city and country when the address can be resolved. They can also record notes, including where they heard about the place.

**Why this priority**: Capturing a researched place independently of a trip is the core value of the feature and creates a useful destination list on its own.

**Independent Test**: Save a destination with a name and address, optionally add notes (including a source or recommendation), then leave and return to the favorites list and confirm the saved details remain available only to that traveler.

**Acceptance Scenarios**:

1. **Given** a signed-in traveler is researching a place, **When** they save its name and address as a favorite, **Then** it appears in their favorite destinations with city and country populated when address resolution succeeds.
2. **Given** the traveler learned about a place from another person or source, **When** they record that in optional notes, **Then** that context is shown when they review the destination.
3. **Given** the traveler has no favorite destinations, **When** they open the favorites area, **Then** they see a clear empty state and an action to save their first destination.
4. **Given** a traveler omits a destination name or address, **When** they attempt to save it, **Then** the save is refused with clear guidance about the missing information.

---

### User Story 2 - Review and Maintain Favorite Destinations (Priority: P2)

A traveler opens Favorites from the account dropdown, reviews their saved places in country/city/name order, searches across destination details, corrects or expands research notes, and removes places they no longer want to consider.

**Why this priority**: A growing research list remains useful only when travelers can find and maintain its entries, but this depends on destinations first being saved.

**Independent Test**: Create several favorites, locate a known one by name or address, edit each saved field, and delete another; confirm the edits persist and the deleted destination no longer appears.

**Acceptance Scenarios**:

1. **Given** a traveler has multiple favorite destinations, **When** they open the favorites area, **Then** they can review each destination's name, address, and available research context.
2. **Given** a traveler has many favorites, **When** they search using text from a destination's name, address, city, country, or notes, **Then** matching destinations are shown and unrelated destinations are excluded.
3. **Given** a saved destination has incomplete or outdated research context, **When** the traveler edits it and saves valid changes, **Then** the updated details are shown on subsequent visits.
4. **Given** a traveler no longer wants a saved destination, **When** they confirm its removal, **Then** it is removed from their favorites without changing any trip items previously created from it.
5. **Given** a traveler scrolls through a long favorites list, **When** they continue browsing, **Then** the search box remains available at the top of the viewport.
6. **Given** a traveler has multiple favorites, **When** they open the list, **Then** entries are sorted by country, then city, then name.

---

### User Story 3 - Import Favorite Destinations (Priority: P2)

A traveler imports a list of researched places from a JSON or CSV file instead of entering each favorite individually.

**Why this priority**: Import reduces the effort of bringing an existing research list into the application while using the same ownership and validation rules as manual entry.

**Independent Test**: Import valid JSON and CSV files, verify every imported favorite belongs to the signed-in traveler and appears with parsed location fields, and verify malformed or invalid files do not create partial data.

**Acceptance Scenarios**:

1. **Given** a traveler has a valid JSON array or CSV file with the documented columns, **When** they import it, **Then** all valid rows are added to their favorites.
2. **Given** an import file contains invalid rows, **When** the traveler submits it, **Then** the response identifies the row and validation errors and no part of the import is committed.
3. **Given** an import includes possible duplicates, **When** the traveler reviews the warnings, **Then** they can explicitly confirm importing the duplicates or cancel without changes.
4. **Given** a traveler imports addresses that can be resolved, **When** the import completes, **Then** city and country are derived using the same rules as manual entry.

---

### User Story 4 - Use a Favorite When Planning a Trip (Priority: P3)

A traveler selects a favorite destination in the tracked-item dialog and starts a new tracked item with reusable details filled in. They complete the trip-specific details using the existing tracked-item workflow before saving.

**Why this priority**: Turning research into an itinerary entry completes the intended lifecycle, while deliberately preserving the existing trip-item rules and planning workflow.

**Independent Test**: Open tracked-item creation on a trip the traveler may modify, select a favorite in the dialog, supply all required trip-specific fields, save, and confirm both the new tracked item and the original favorite remain.

**Acceptance Scenarios**:

1. **Given** a traveler is creating a tracked item on a trip they may modify, **When** they select one of their favorites in the dialog, **Then** its name and address populate the tracked-item fields.
2. **Given** a favorite has notes, **When** it is selected, **Then** its notes are also prefilled while trip-specific fields remain available for review and completion.
3. **Given** the traveler completes all details required by the existing tracked-item workflow, **When** they save, **Then** a new tracked item is added to the trip and the favorite remains available for future use.
4. **Given** a selected trip leg is ineligible or its window is violated, **When** the traveler attempts to save, **Then** the existing tracked-item validation rejects the item with its established guidance.
5. **Given** the traveler changes prefilled details before saving, **When** the tracked item is created, **Then** those changes apply to the new item without altering the saved favorite.
6. **Given** the traveler cancels item creation, **When** they return to favorites, **Then** no tracked item is created and the favorite remains unchanged.

### Edge Cases

- A traveler saves a place whose normalized name and address match an existing favorite; the system warns about the possible duplicate but permits both entries because similarly named places and intentionally separate research notes are valid.
- A favorite contains long notes; the traveler can review and edit the full content without the list becoming unusable.
- A favorite's address is free-form, ambiguous, or cannot be resolved by the configured address lookup; the address can still be saved and city, country, latitude, and longitude remain blank until a later address change resolves.
- A JSON or CSV import contains malformed syntax, missing required fields, or possible duplicates; the traveler receives row-level feedback and no partial import is committed.
- A trip is deleted or the traveler loses edit access between choosing it and saving the tracked item; item creation is refused without changing the favorite.
- A selected leg becomes ineligible or is removed while the item form is open; eligibility is checked again when the item is saved.
- A favorite is edited or deleted after one or more trip items were created from it; those existing trip items remain unchanged.
- A traveler attempts to view or modify another traveler's favorites; no favorite details are revealed and no change is allowed.
- A save, edit, or deletion fails; the traveler receives clear feedback and previously saved destination data remains intact.

## Requirements *(mandatory)*

### Functional Requirements

**Favorite capture and ownership**

- **FR-001**: The system MUST allow a signed-in traveler to save a favorite destination independently of any trip.
- **FR-002**: Each favorite destination MUST have a traveler-provided name and address.
- **FR-003**: The system MUST allow a traveler to record optional notes, which also hold any source or recommendation, for a favorite destination.
- **FR-004**: The system MUST refuse a favorite that lacks a non-blank name or address and MUST identify each missing field.
- **FR-005**: The system MUST scope favorite destinations to the traveler who saved them.
- **FR-006**: The system MUST prevent a traveler from viewing, changing, deleting, or using another traveler's favorite destination.
- **FR-007**: When a new favorite has the same normalized name and address as an existing favorite belonging to that traveler, the system MUST warn about the possible duplicate and MUST allow the traveler either to continue or cancel.

**Review and maintenance**

- **FR-008**: The system MUST provide a Favorites item in the signed-in user's account dropdown and a dedicated page that lists all favorite destinations belonging to that traveler.
- **FR-009**: Each favorite in the list MUST show its name and address and MUST provide access to its complete saved research context.
- **FR-010**: The system MUST show a clear empty state with an action to add a favorite when the traveler has none.
- **FR-011**: The system MUST allow the traveler to find favorites by case-insensitive text contained in the name, address, parsed city, parsed country, or notes.
- **FR-012**: The system MUST allow the traveler to edit the name, address, and notes of a favorite, subject to the same validation used at creation.
- **FR-013**: The system MUST allow the traveler to delete a favorite after confirming the action.

**Creating a trip item**

- **FR-014**: The system MUST allow a traveler to select one of their favorite destinations from the tracked-item creation dialog.
- **FR-015**: The system MUST limit destination trips to trips the traveler is currently permitted to modify under existing ownership and collaboration rules.
- **FR-016**: Selecting a favorite MUST prefill the new tracked item's title, address/location, and notes when present, while allowing the traveler to review and change them before saving.
- **FR-017**: The system MUST require all trip-specific details required by the existing tracked-item workflow and MUST enforce its schedule and selected-leg eligibility rules before saving. An unassigned leg remains allowed when the existing workflow allows it.
- **FR-018**: The system MUST apply existing tracked-item validation, date and time rules, leg-eligibility rules, and trip permissions when saving an item initiated from a favorite.
- **FR-019**: Creating, changing, or deleting a tracked item initiated from a favorite MUST NOT change or delete the favorite.
- **FR-020**: Editing or deleting a favorite MUST NOT change or delete any tracked item previously created from it.
- **FR-021**: Canceling or failing tracked-item creation MUST leave the favorite unchanged and MUST NOT create a partial trip item.
- **FR-022**: The system MUST automatically derive city, country, latitude, and longitude from the entered address when the configured address resolver returns them. Name and address MUST be the only user-editable location fields; the entered address MUST remain the canonical address.
- **FR-023**: If address resolution is unavailable or cannot determine the location, the system MUST preserve the address and leave the unresolved calculated fields blank. When an update changes the address, the system MUST clear the previously calculated city, country, latitude, and longitude and recalculate them from the new address.
- **FR-024**: The favorites list MUST sort by country, then city, then name, using case-insensitive ordering and a deterministic tie-breaker; missing city/country values MUST sort after populated values.
- **FR-025**: The favorites page MUST keep its single search box sticky at the top while the list scrolls.
- **FR-026**: The system MUST support bulk import from JSON and CSV. JSON MUST be an array of records; CSV MUST use the headers `name,address,notes` with standard quoted-field escaping.
- **FR-027**: Bulk import MUST validate all rows before writing. Invalid rows MUST be reported with row-specific errors, possible duplicates MUST be explicitly confirmable, and the import MUST be all-or-nothing.
- **FR-028**: Imported favorites MUST be owned by the signed-in traveler, use the same address parsing and validation as manually created favorites, and remain inaccessible to other travelers.

### Key Entities

- **Favorite Destination**: A traveler-owned researched place that exists independently of any trip. It has a name, address, calculated city/country/coordinates, optional notes, and creation and last-updated timestamps.
- **Traveler**: The signed-in person who owns and privately maintains favorite destinations and may have permission to modify selected trips.
- **Trip**: An existing travel plan that may receive a new tracked item when the traveler has edit permission.
- **Tracked Item**: A dated itinerary entry created through the existing trip-planning workflow. It receives reusable favorite details as initial values but remains independent after creation.
- **Trip Leg**: The portion of a trip to which a new tracked item is assigned, subject to existing timeframe and eligibility rules.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of test participants can save a researched destination with its required details on their first attempt in under 90 seconds.
- **SC-002**: A traveler with 500 saved destinations can open the favorites area and see a usable list within 2 seconds under normal operating conditions.
- **SC-003**: At least 95% of searches by an exact or partial saved name, address, city, country, or notes display the expected favorite in the first result set.
- **SC-004**: At least 90% of test participants can start with a favorite and successfully create a valid tracked item on an editable trip in under 3 minutes.
- **SC-005**: In all acceptance tests, creating, editing, or deleting a favorite leaves previously created trip items unchanged, and creating or editing a trip item leaves the favorite unchanged.
- **SC-006**: In all authorization tests, travelers are unable to view or alter favorite destinations owned by another traveler.
- **SC-007**: At least 85% of test participants report that saving and later using researched destinations is clear without assistance.
- **SC-008**: In all import acceptance tests, valid JSON and CSV batches are fully imported, while files with invalid rows or unconfirmed duplicates create no partial data.

## Assumptions

- Favorite destinations are personal research records, not part of a trip and not shared through trip-sharing permissions.
- A destination's name and free-form address are the minimum required identifying details; city, country, and coordinates are calculated from the address and are not user-editable; notes are optional.
- Where a traveler heard about a place (a person's name, publication, or link) is recorded in notes; retrieving or verifying external content is outside this feature.
- Choosing a favorite starts the existing tracked-item creation experience rather than creating an item immediately.
- A favorite remains saved after it is used and may be reused on multiple trips.
- The new tracked item copies relevant text from the favorite; no ongoing synchronization or durable relationship is required.
- Ratings, categories, images, public discovery, and sharing favorite lists are outside the initial feature scope.
