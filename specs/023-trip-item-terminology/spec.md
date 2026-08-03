# Feature Specification: Trip Leg Item Terminology

**Feature Branch**: `023-trip-item-terminology`

**Created**: 2026-08-03

**Status**: Draft

**Input**: User description: "trip legs have children elements called events. However, an event is only one type of child item, so the name is not accurate anymore."

## Overview

Trip legs hold child entries that can be one of four types: **Event**, **Reservation**, **Activity**, or **Reminder**. The product's wording, however, calls the whole collection "events" — the same word used for one specific type. A traveler adding a hotel reservation is told to "Add event", sees the leg summary read "3 events", and is warned that "every event must be related to a trip leg". The word means two different things depending on where it appears, which makes the type selector look redundant and the counts look wrong.

This feature adopts **item** (plural **items**) as the single name for a trip leg's child entries, replacing every generic use of "event" while keeping "Event" reserved for the one type that genuinely carries that name. The rename is applied end to end — traveler-facing text, internal code names, database objects, and API field names — so a single vocabulary holds throughout the product.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Planning surfaces call leg children "items" (Priority: P1)

A traveler opens a trip, looks at the timeline and the itinerary summary, and adds a hotel reservation to a leg. Every label, button, count, and empty-state message calls the child entries "items", so nothing implies the traveler is creating an "event" when they are creating a reservation.

**Why this priority**: These are the highest-traffic screens in the product and the place where the collision is most visible and most confusing. Fixing them alone removes the majority of the ambiguity.

**Independent Test**: Open a trip with a mix of item types, review the timeline, the itinerary summary counts, the add action, the empty states, and the printable trip. Confirm no generic use of "event" remains and that the counts read naturally.

**Acceptance Scenarios**:

1. **Given** a trip leg with one reservation and one activity, **When** the traveler views the leg in the timeline, **Then** the leg summary reads "2 items" and never "2 events".
2. **Given** a trip leg, **When** the traveler chooses the action that adds a child entry, **Then** the action label and its accessible name read "Add item".
3. **Given** a trip with no child entries on a leg, **When** the traveler views that leg, **Then** the empty-state message refers to items.
4. **Given** a trip with items not related to any leg, **When** the traveler views the timeline, **Then** the notice about unrelated entries reads "1 item is not related to a trip leg" or "N items are not related to a trip leg" as appropriate.
5. **Given** a trip, **When** the traveler opens the printable version, **Then** all headings and empty-state text refer to items.

---

### User Story 2 - Adding and editing says "item", and "Event" means only the type (Priority: P2)

A traveler opens the add/edit form for a leg item. The form's title, field labels, and any validation messages call the thing being created an item. The type selector still offers "Event" as one of four choices, and the type badge on a saved item still reads "Event" when that type was chosen.

**Why this priority**: This is where the collision is most jarring — the form calls the record an "event" while simultaneously asking the traveler to pick "Event" from a list of four types. It depends on the vocabulary chosen in User Story 1 but is independently demonstrable.

**Independent Test**: Open the add form, submit it with invalid values, and inspect the form title, field labels, accessible names, and every validation message. Confirm the only remaining "Event" wording is the type option and the type badge.

**Acceptance Scenarios**:

1. **Given** the add form is open, **When** the traveler reads the form title and field labels, **Then** they refer to an item.
2. **Given** the add form is open, **When** the traveler reads the type selector, **Then** "Event" is still offered as one of the four type choices.
3. **Given** a trip with no legs, **When** the traveler tries to add an item, **Then** the guidance message refers to items.
4. **Given** the traveler submits a start or end outside the selected leg's travel window, **When** the validation message is shown, **Then** it refers to the item.
5. **Given** a saved item whose type is Event, **When** the traveler views it, **Then** its type badge reads "Event".

---

### User Story 3 - Notifications and email-review say "item" (Priority: P3)

A traveler who shares a trip receives a change notification, and a traveler who forwards a booking email reviews the parsed results. Both describe the affected records as items rather than calling a parsed hotel booking an "event".

**Why this priority**: These surfaces reach the traveler outside the app, where a mismatch with in-app wording is more confusing than helpful, but they are seen far less often than the planning screens.

**Independent Test**: Trigger a create, update, and delete notification on a shared trip, and open the parsed-email review screen with at least one pending draft. Confirm all wording says "item".

**Acceptance Scenarios**:

1. **Given** a shared trip, **When** a collaborator adds, changes, or removes an item, **Then** the resulting notification describes the change in terms of an item.
2. **Given** one or more parsed drafts are pending, **When** the traveler is notified, **Then** the notification subject and body refer to items with correct singular and plural wording.
3. **Given** the review screen has no pending drafts, **When** the traveler opens it, **Then** the empty-state message refers to items.
4. **Given** a pending draft has no trip assigned, **When** the traveler tries to confirm it, **Then** the guidance message refers to the item.

---

### User Story 4 - Help and informational content is consistent (Priority: P4)

A traveler reads the FAQ and About pages to understand how trips are structured. The vocabulary there matches what they see on the planning screens.

**Why this priority**: Informational content is read rarely and does not block any task, but leaving it stale would reintroduce the very confusion this feature removes.

**Independent Test**: Read the FAQ and About pages end to end and confirm every reference to leg children says "item" and that the four types are described accurately.

**Acceptance Scenarios**:

1. **Given** the FAQ page, **When** the traveler reads the answers about trip structure, timezones, date ranges, maps, and deletion, **Then** each refers to items.
2. **Given** the About page, **When** the traveler reads the product description, **Then** it uses the same "item" vocabulary as the planning screens.

---

### User Story 5 - One vocabulary from screen to database (Priority: P5)

A developer reading the codebase finds a single vocabulary: the traveler-facing labels, the code type and member names, the API request and response fields, and the database objects all say "item". No layer requires mentally translating "event" into "item".

**Why this priority**: This carries no traveler-visible benefit and is the riskiest part of the change, so it lands last. It is what prevents the ambiguity from creeping back in through future work that copies existing names.

**Independent Test**: Search the codebase, API contracts, and database schema for "event". Confirm every remaining hit denotes the specific Event type, and that the application starts, reads existing data, and passes its full test suite.

**Acceptance Scenarios**:

1. **Given** the codebase after the change, **When** a developer searches for "event" outside of type definitions, **Then** no generic uses remain.
2. **Given** a database populated before the change, **When** the migrated application starts, **Then** every existing trip, leg, and item loads with its original values and relationships intact.
3. **Given** an item saved as the Event type before the change, **When** it is loaded after the change, **Then** it is still recognized and displayed as the Event type.

---

### Edge Cases

- A count of exactly one must read "1 item"; a count of zero and counts above one must read "0 items" and "N items". Placeholder forms such as "item(s)" are not acceptable.
- The word "Event" must still appear wherever it denotes the specific type: the type selector, type badges, type icons' accessible names, and any type-based filtering or grouping.
- Screen-reader-only text, `title` tooltips, accessible names, and browser page titles are user-visible and must be renamed alongside visible labels.
- The word "item" must not become ambiguous with unrelated uses; where context is thin, such as a page title or a notification subject, the wording should qualify it (for example "trip item").
- Existing items created before the change must display under the new wording, and the database migration must preserve every row, type value, and leg relationship.
- Existing bookmarked or shared trip URLs must continue to resolve unchanged.
- Renamed database objects and renamed API fields must ship together with the application that consumes them, since old and new names will not coexist.
- Automated tests and fixtures that assert on the old wording or the old field names will fail until updated; they are part of this change, not follow-up work.
- Wording used purely for internal diagnostics (logs, traces) is not traveler-facing, but is renamed with the code it belongs to so the vocabulary stays uniform.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The product MUST use **item** (plural **items**) as the single name for the child entries of a trip leg, in every traveler-facing surface.
- **FR-002**: The word "Event" MUST appear in traveler-facing text only where it denotes the specific Event type, and MUST NOT be used to describe the collection or any item of another type.
- **FR-003**: All counts and summaries of leg items MUST read "0 items", "1 item", and "N items" with grammatically correct singular and plural forms.
- **FR-004**: All traveler-facing validation and error messages produced when creating, editing, or deleting a leg item MUST refer to it as an item.
- **FR-005**: All change notifications sent to trip collaborators MUST describe created, updated, and deleted leg items as items.
- **FR-006**: All screens and notifications for reviewing entries parsed from forwarded email MUST refer to items, including empty states and confirmation guidance.
- **FR-007**: Help and informational content, including FAQ answers and the About page, MUST refer to items and MUST accurately name the four available types.
- **FR-008**: Accessible names, `title` tooltips, screen-reader-only text, and browser page titles MUST use the same "item" vocabulary as the visible labels they accompany.
- **FR-009**: The set of available types MUST remain Event, Reservation, Activity, and Reminder, with no additions, removals, or renames.
- **FR-010**: Stored traveler data MUST be preserved: existing items, their types, and their relationships to legs MUST continue to display and behave exactly as before.
- **FR-011**: Existing trip and page URLs MUST continue to resolve, so bookmarked and shared links are not broken.
- **FR-012**: No behavior changes MUST be introduced: every create, edit, delete, share, notify, print, and email-import flow MUST work exactly as it did before the rename.
- **FR-013**: The rename MUST extend beyond traveler-facing text to internal names — code types and members, API request and response field names, and database objects — so that the generic use of "event" is eliminated at every layer.
- **FR-014**: Breaking changes to the API contract are acceptable, since the API has no consumers outside this product; the web application MUST be updated in the same change so the two stay in step.
- **FR-015**: Renaming database objects MUST be delivered as a migration that preserves all existing rows, type values, and relationships, and that requires no action from the traveler.
- **FR-016**: The stored values that identify a type MUST NOT change, so an item saved as the Event type is still recognized as the Event type after the change.
- **FR-017**: After the change, no part of the product MUST use "event" to mean a trip leg's child entry. Remaining occurrences are limited to the specific Event type, and to unrelated concepts that genuinely are events — security audit records and the notification deduplication key — which are out of scope.

### Key Entities

- **Trip**: A planned journey with a title, a date range, and an owner. Contains trip legs and their items.
- **Trip Leg**: A dated segment of a trip with an origin, a destination, and its own travel window. Owns a collection of items.
- **Item**: A single planned entry inside a leg — the entity whose name this feature corrects. Has a title, an optional location, a start and an end within the leg's travel window, a color, an optional confirmation code, optional notes, an optional estimated cost, and exactly one **Type**.
- **Type**: The classification of an item. One of Event, Reservation, Activity, or Reminder. "Event" is one value of this attribute, not the name of the entity.
- **Parsed Draft**: An item extracted from a forwarded email and held for the traveler to review, assign to a trip and leg, and confirm.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero traveler-facing strings in the product use "event" or "events" to mean anything other than the specific Event type.
- **SC-002**: Every traveler-facing surface that refers to leg items — planning screens, add/edit form, validation messages, notifications, email review, printable trip, and help content — uses the word "item".
- **SC-003**: In a review of count displays at zero, one, and multiple items, 100% read as grammatically correct sentences with no placeholder plural forms.
- **SC-004**: In a first-read comprehension check, at least 9 of 10 participants correctly identify that a hotel reservation is one of a leg's items.
- **SC-005**: All previously passing create, edit, delete, share, notify, print, and email-import scenarios still pass, confirming zero behavior regressions.
- **SC-006**: No traveler action is required after the change: 100% of existing trips, legs, and items display correctly on first load with no data migration prompt or error.
- **SC-007**: A search of the codebase, API contracts, and database schema returns zero uses of "event" meaning a trip leg's child entry; every remaining occurrence denotes either the specific Event type or an unrelated concept that genuinely is an event.

## Assumptions

- The four types — Event, Reservation, Activity, Reminder — are correct and stay as they are; only the collective name is wrong.
- The product is English-only with no localization layer, so the rename is a single-language change.
- The rename is a naming change, not a feature change: no fields are added or removed, and no screens are added or removed.
- The API has no consumers outside this product, so renaming request and response fields is safe and needs no versioning or deprecation period.
- Much of the backend already leans on the "item" vocabulary, so the remaining internal renames are bounded rather than pervasive.
- A database migration is acceptable and will be applied automatically at deployment; it renames objects only and does not alter stored values.
- Existing automated tests that assert on the old wording or the old field names will be updated as part of this change.
- There is no end-user documentation outside the application, so no external content needs to be republished.
- Notifications already sent before the change keep their original wording; only newly generated messages use the new noun.
