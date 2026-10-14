# Feature Specification: Mobile Entry Pages

**Feature Branch**: `031-mobile-entry-pages`
**Created**: 2026-05-20
**Status**: Draft
**Input**: User description: "I'd like to improve the mobile aspects of this site with less dialogs and more individual pages to handle data entry."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Add and edit trip legs and tracked items on their own pages (Priority: P1)

A traveler on a phone opens a trip and taps "Add leg" or "Add item". Instead of a dialog squeezed over the trip, a full page opens with a clear title, the form fields, and Save and Cancel actions. After saving, they return to the trip.

**Why this priority**: Legs and tracked items are the most frequent data entry on the site, and dialogs are hardest to use on small screens.

**Independent Test**: On a 360px-wide screen, add and edit a leg and a tracked item and confirm each happens on its own page and returns to the trip.

**Acceptance Scenarios**:

1. **Given** a trip is open on a phone, **When** the user taps Add leg, **Then** a dedicated page opens showing the trip name as context and the leg form.
2. **Given** the leg page is open, **When** the user saves valid data, **Then** the leg is stored and the user returns to the trip showing the new leg.
3. **Given** the page is open, **When** the user taps Cancel or Back, **Then** nothing is saved and they return to the trip.
4. **Given** invalid data, **When** the user saves, **Then** errors appear beside the fields and entered values are preserved.

---

### User Story 2 - Create and edit trips on pages (Priority: P1)

A user creates or edits a trip on a dedicated page reachable from the trips list or trip details.

**Why this priority**: Trip creation is the entry point to the whole product.

**Independent Test**: Create and edit a trip on a phone without any dialog appearing.

**Acceptance Scenarios**:

1. **Given** the trips list, **When** the user taps New trip, **Then** a full page form opens.
2. **Given** a trip, **When** the user chooses Edit, **Then** a page opens prefilled and saving returns to where they came from.

---

### User Story 3 - Add, edit and import favorite destinations on pages (Priority: P2)

A user manages favorites through dedicated pages for add, edit and import.

**Why this priority**: Used less often than trips but has the same usability problem.

**Independent Test**: Add, edit and import a favorite on a phone using only pages.

**Acceptance Scenarios**:

1. **Given** the favorites list, **When** the user taps Add, Edit or Import, **Then** a dedicated page opens and saving returns to the list.

---

### User Story 4 - Edit email drafts on a page (Priority: P2)

A user reviewing inbox drafts opens a draft to edit on its own page, then returns to the inbox.

**Why this priority**: Drafts contain many fields, which are cramped in a dialog.

**Independent Test**: Edit and save a draft on a phone, returning to the inbox.

**Acceptance Scenarios**:

1. **Given** the drafts inbox, **When** the user chooses Edit on a draft, **Then** a dedicated page opens and saving returns to the inbox.

---

### User Story 5 - Share a trip on a page (Priority: P3)

A user manages trip sharing on a dedicated page opened from the trip.

**Why this priority**: Occasional use.

**Independent Test**: Open sharing from a trip, make a change, and return to the trip.

**Acceptance Scenarios**:

1. **Given** a trip the user owns, **When** they choose Share, **Then** a dedicated page opens and Back returns to the trip.

---

### Edge Cases

- Opening an entry page by direct link, reload or bookmark works and shows the correct form.
- Leaving with unsaved changes prompts the user to confirm.
- Users with view-only access cannot reach edit pages and see a clear message.
- A record deleted elsewhere shows a friendly not-found message with a way back.
- Double-tapping Save does not create duplicates.
- The on-screen keyboard must not hide the Save action or the focused field.
- Map views, delete confirmations and the reconnect notice remain overlays.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide dedicated pages for creating and editing trips, trip legs, tracked items, favorite destinations, favorite import, email drafts, and trip sharing, replacing the current dialogs.
- **FR-002**: Each entry page MUST show a title, context of the parent record, and Save and Cancel actions.
- **FR-003**: Saving or cancelling MUST return the user to the page they came from, or a sensible default if opened directly.
- **FR-004**: Each entry page MUST have a stable address that works via direct link, reload and browser back.
- **FR-005**: Validation errors MUST appear inline and preserve entered input.
- **FR-006**: The system MUST warn users before discarding unsaved changes.
- **FR-007**: Entry pages MUST be usable on phone screens (about 360px wide) without horizontal scrolling, with touch-friendly controls.
- **FR-008**: Existing permissions MUST be enforced on entry pages.
- **FR-009**: Fields, validation rules and saved results MUST be unchanged from the current forms.
- **FR-010**: The same page-based flow MUST be used on all screen sizes.
- **FR-011**: Delete confirmations, map views and the reconnect notice MAY remain overlays.
- **FR-012**: A missing or deleted record MUST show a friendly message with navigation back.

### Key Entities

- **Trip**: a planned journey that is created and edited.
- **Trip Leg**: a segment of a trip.
- **Tracked Item**: an item tracked within a trip.
- **Favorite Destination**: a saved place a user wants to revisit.
- **Email Draft**: a parsed email awaiting review.
- **Trip Share**: access granted to others for a trip.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the listed data-entry forms open as pages rather than dialogs.
- **SC-002**: A user can add a leg on a 360px-wide screen in under 60 seconds.
- **SC-003**: At least 95% of users complete a data-entry task on a phone on the first attempt.
- **SC-004**: 100% of saves and cancels return the user to the originating page.
- **SC-005**: Browser back works correctly from every entry page.
- **SC-006**: No existing data-entry capability is lost.

## Assumptions

- The page-based flow applies to all screen sizes for consistency.
- Existing forms, validation and permissions are reused.
- Overlays are retained for confirmations, maps and notices.
- Offline entry is out of scope.
