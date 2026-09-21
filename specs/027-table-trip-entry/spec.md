# Feature Specification: Editable Itinerary Table View

**Feature Branch**: `main`

**Created**: 2026-09-21

**Status**: Draft

**Input**: User description: "In addition to the timeline view, provide a table view of trip legs and tracked items. Reuse the existing modals rather than recreating entry functionality. Make the table maximally shareable with the printable itinerary: trip legs are full-width rows and tracked items occupy the columns beneath their leg."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View the Itinerary as a Hierarchical Table (Priority: P1)

A traveler can switch between the timeline and a compact table that resembles the printable itinerary. Each trip leg is a full-width divider row, and its tracked items appear immediately beneath it with their values aligned in columns.

**Why this priority**: The hierarchical table is the core requested view and gives travelers a dense, scannable representation of the complete itinerary.

**Independent Test**: Open a trip containing multiple legs, assigned items, and unassigned items; switch from timeline to table and verify the table uses chronological leg dividers, column-aligned item rows, and an unassigned section without changing trip data.

**Acceptance Scenarios**:

1. **Given** a trip has several legs, **When** the traveler selects Table view, **Then** each leg appears as a full-width row in chronological order.
2. **Given** a leg has tracked items, **When** the table is shown, **Then** those items appear directly beneath that leg and align under labeled columns.
3. **Given** a leg has no items, **When** the table is shown, **Then** the leg remains visible and clearly indicates that it has no items.
4. **Given** a tracked item is unassigned or refers to a missing leg, **When** the table is shown, **Then** it appears in a distinct full-width Unassigned section after the trip legs.
5. **Given** the traveler switches between Timeline and Table views, **When** either view is selected, **Then** both present the same current trip legs and tracked items.

---

### User Story 2 - Edit Table Entries Through Existing Modals (Priority: P2)

A traveler selects a leg divider or tracked-item row in the table and edits it through the same modal already used by the timeline. Add actions also open the existing create modals, preserving one set of forms, validation rules, and save behavior.

**Why this priority**: The table becomes an editable alternative without duplicating complex entry logic or allowing the timeline and table to drift apart.

**Independent Test**: From Table view, select a leg and an item, verify the existing edit modals open with their saved values, save changes, then add a leg and item through existing create modals and verify the table refreshes.

**Acceptance Scenarios**:

1. **Given** a traveler can edit the trip, **When** they select a leg divider row, **Then** the existing leg edit modal opens for that leg.
2. **Given** a traveler can edit the trip, **When** they select a tracked-item row, **Then** the existing tracked-item edit modal opens for that item.
3. **Given** an eligible leg divider, **When** the traveler chooses its add-item action, **Then** the existing create-item modal opens with that leg preselected.
4. **Given** Table view is active, **When** the traveler chooses Add leg or Add item, **Then** the same create modal used by the timeline opens.
5. **Given** a modal save succeeds, **When** the modal closes, **Then** both Table and Timeline views reflect the saved trip data without a page reload.
6. **Given** a modal save fails validation, **When** feedback is shown, **Then** the existing modal retains the entered values and behavior; the table does not implement separate validation.

---

### User Story 3 - Share Presentation with the Printable Itinerary (Priority: P3)

The interactive table and printable itinerary use the same row hierarchy, columns, ordering, labels, and formatting so that the on-screen table is an editable counterpart of the printout rather than a second interpretation of trip data.

**Why this priority**: Maximum presentation sharing reduces inconsistency and makes the table immediately familiar to travelers who use the print view.

**Independent Test**: Render the same populated trip in Table view and Print view and verify that leg/item grouping, ordering, columns, labels, date/time-zone formatting, booking details, costs, empty-leg rows, and unassigned items match; interactive actions appear only on screen.

**Acceptance Scenarios**:

1. **Given** the same trip is shown on screen and in Print view, **When** their itinerary tables are compared, **Then** they use the same leg grouping, item ordering, column definitions, labels, and formatted values.
2. **Given** an interactive-only edit or add action is available in Table view, **When** the trip is printed, **Then** that action is omitted without changing the data columns.
3. **Given** the itinerary has no legs or items, **When** either presentation is shown, **Then** both use a consistent empty state.
4. **Given** the shared table presentation changes in the future, **When** Table and Print views render it, **Then** the change applies to both unless explicitly designated as interactive-only or print-only.

---

### User Story 4 - Use Table Entry Across Supported Devices and Access Needs (Priority: P4)

A traveler can understand and operate table-based planning with a keyboard, assistive technology, or a smaller screen without fields overlapping or essential actions becoming unreachable.

**Why this priority**: A dense table only provides value when it remains usable for the product's supported audiences and screen sizes.

**Independent Test**: Select leg and item rows using keyboard-only navigation and a screen reader, then inspect the table on a supported small-screen viewport and verify every value and action remains understandable and reachable.

**Acceptance Scenarios**:

1. **Given** a keyboard-only traveler who can edit the trip, **When** they navigate the table, **Then** every selectable row and add action is reachable in a predictable order with a visible focus indicator.
2. **Given** a traveler using assistive technology, **When** they encounter a leg divider or item row, **Then** its hierarchy, column meaning, current value, and available action are announced.
3. **Given** the available width cannot present all columns legibly, **When** the traveler uses table entry, **Then** all values and actions remain accessible through an adapted row presentation without text or controls overlapping.

### Edge Cases

- A trip has no legs or items; each table presents an empty state and a clear action to add its first eligible row.
- A trip has many legs or items; stable chronological ordering is retained and the traveler can reach every row without losing their current edit.
- Two rows have the same start time; their ordering remains stable and predictable.
- A traveler changes a leg's classification or transportation mode in a way that would make its assigned items ineligible; the existing protection against stranding items applies before the change is saved.
- A traveler changes a leg's timeframe so one or more items no longer fit; existing timeframe validation and warning behavior remains in force.
- A modal save fails because connectivity is interrupted; the existing form retains the traveler's values and the table continues to show the last saved data.
- Another collaborator changes or removes the same leg or item while the traveler is editing; stale data is not allowed to silently replace the newer state.
- A row contains long titles, locations, notes, or confirmation numbers; the traveler can inspect and edit the full value without breaking the table layout.
- The traveler lacks edit permission or loses it while editing; the attempted change is refused without exposing or altering unauthorized data.

## Requirements *(mandatory)*

### Functional Requirements

**Table alternative and hierarchy**

- **FR-001**: The trip detail experience MUST offer table-based planning as an alternative to the existing trip views without removing or replacing those views.
- **FR-002**: The table-based planning experience MUST present one hierarchical itinerary table, not separate leg and item tables.
- **FR-003**: The itinerary table MUST show each trip leg as a full-width divider row in chronological order.
- **FR-004**: The itinerary table MUST show each leg's tracked items immediately beneath that leg as rows aligned to labeled item columns and ordered chronologically.
- **FR-005**: Items that are unassigned or reference no current leg MUST appear beneath a distinct full-width Unassigned divider after all leg groups.

**Shared presentation**

- **FR-006**: Table and Print views MUST share the same itinerary table presentation for leg grouping, item ordering, column definitions, labels, and formatted data values.
- **FR-007**: Each leg divider MUST display the leg title, applicable transportation mode and route, start and end with time zones, and applicable confirmation and travel-cost details.
- **FR-008**: Item columns MUST display type, title, location, start, end, confirmation, and estimated cost using the same formatting in Table and Print views.
- **FR-009**: A leg with no tracked items MUST remain visible and MUST have a consistent no-items row in Table and Print views.
- **FR-010**: Interactive controls and edit affordances MUST be omitted from printed output without changing the shared data columns or hierarchy.

**Modal-based editing**

- **FR-011**: Travelers with edit permission MUST be able to select a leg divider to open the existing leg edit modal for that leg.
- **FR-012**: Travelers with edit permission MUST be able to select a tracked-item row to open the existing tracked-item edit modal for that item.
- **FR-013**: Table-level Add leg and Add item actions MUST open the same create modals used by the timeline.
- **FR-014**: Each item-eligible leg divider MUST offer an add-item action that opens the existing create-item modal with that leg preselected.
- **FR-015**: Table view MUST NOT recreate leg or item form fields, validation, save, cancel, eligibility, or stale-data behavior.
- **FR-016**: After a successful modal save, the trip detail page MUST refresh the shared trip data so both Table and Timeline views reflect the change without a full page reload.

**Access and interaction**

- **FR-017**: Travelers with view-only access MUST be able to inspect Table view but MUST NOT be offered or permitted interactive add or edit actions.
- **FR-018**: Selecting or invoking an action on one table row MUST NOT change any other leg or item except through the existing modal workflow after a successful save.
- **FR-019**: Table view MUST use the same current trip-detail data as the surrounding page and MUST NOT introduce table-specific persisted or draft records.

**Usability and accessibility**

- **FR-020**: Selectable rows and add actions MUST be fully operable by keyboard with a predictable navigation order and visible focus.
- **FR-021**: The table MUST use correct row-group and column semantics so assistive technology can identify each leg divider, its child items, column meanings, and interactive purpose.
- **FR-022**: When the available width cannot show all columns legibly, the experience MUST preserve a real table and provide contained horizontal scrolling so every column remains aligned, reachable, and non-overlapping.
- **FR-023**: Long values MUST remain inspectable without causing unrelated columns or actions to become unusable.

### Key Entities

- **Trip**: The travel plan being viewed or edited. It owns the legs and tracked items shown by both tables and controls traveler access.
- **Trip Leg**: A Travel or Stay segment represented as one leg-table row, with route or stay details, a travel window, time zones, and eligibility rules for tracked items.
- **Tracked Item**: An event, reservation, activity, or reminder represented as one item-table row and assigned to one eligible leg or retained as unassigned under existing rules.
- **Itinerary Table Projection**: A transient, shared presentation of trip metadata, chronologically ordered leg groups, column-aligned tracked items, and unassigned items. It retains record identifiers so interactive rows can open the correct modal.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of travelers in usability testing can find and open the table-based alternative without guidance.
- **SC-002**: 100% of legs, assigned items, and unassigned items shown in Print view appear in the same hierarchy and order in Table view.
- **SC-003**: 100% of shared itinerary columns, labels, and formatted data values match between Table and Print views for the same trip.
- **SC-004**: At least 90% of tested travelers successfully open the correct existing modal by selecting a leg or item row on their first attempt.
- **SC-005**: A traveler can start adding a leg or an item from Table view in no more than two actions.
- **SC-006**: 100% of successful modal saves update Table and Timeline views without a full page reload.
- **SC-007**: Zero leg or item form fields, validation rules, or save workflows are duplicated in the table implementation.
- **SC-008**: In keyboard, assistive-technology, and supported small-screen acceptance tests, 100% of table values and available actions remain reachable, understandable, aligned, and non-overlapping.

## Assumptions

- Table-based planning is an optional alternative within a trip, not a replacement for the timeline, calendar, modal forms, printable view, or item detail experiences.
- Legs and tracked items are presented in one hierarchical table matching the existing printout: legs are full-width group rows and items occupy the labeled columns beneath them.
- "Editable" means table rows launch the existing create and edit modals; cells are not inline form controls.
- The shared itinerary table owns hierarchy and row rendering, while a print wrapper owns trip metadata and an interactive wrapper supplies optional selection and add callbacks.
- The table creates and edits the same leg and item records used by existing views; it does not introduce duplicate table-only records.
- Existing rules for trip access, leg classification, transportation modes, item eligibility, time zones, date ranges, costs, and unassigned items remain authoritative.
- The table supports creating and editing records through existing modals. Inline cell editing, destructive bulk operations, spreadsheet import or export, copy and paste across multiple cells, column customization, filtering, sorting by arbitrary columns, and multi-row selection are out of scope for this feature.
- Deleting legs or items continues through the existing protected deletion experience rather than introducing inline or bulk deletion in the first table-based version.
- A responsive adapted row presentation is acceptable on small screens; preserving access and comprehension is more important than forcing a desktop-style grid into limited width.

## Dependencies

- Existing trip detail navigation and permission model.
- Existing trip-leg creation, editing, classification, transportation-mode, and validation behavior.
- Existing tracked-item creation, editing, assignment, unassigned-item, and validation behavior.
- Existing timeline, calendar, and detail views that consume the same saved trip data.

## Out of Scope

- Replacing existing trip views or modal entry forms.
- Inline cell editing or separate table-specific entry forms.
- Bulk import, export, deletion, reassignment, or editing of multiple selected rows in one action.
- Spreadsheet formulas, fill handles, arbitrary cell ranges, or offline spreadsheet synchronization.
- User-defined columns, saved table layouts, or arbitrary sorting and filtering.
- Changing the fields, classifications, transportation modes, item types, or eligibility rules defined by existing features.