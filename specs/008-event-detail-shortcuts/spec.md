# Feature Specification: Event Detail Fields and Quick-Fill Shortcuts

**Feature Branch**: `[008-event-detail-shortcuts]`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "Additional details are needed on the event contents to match the necessary use of this feature. We'll be editing the fields and adding a shortcut for some fields."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capture Start and End Timezone Selections on an Event (Priority: P1)

A traveler opens an event on their trip and selects the timezone for the event's start and, when the event has an end, the timezone for its end — so the event's timing is unambiguous even when a trip leg spans regions or crosses timezone boundaries.

**Why this priority**: The core purpose of this feature is that an event must carry enough timing detail to be used correctly across a trip whose legs vary in timezone. Without explicit start and end timezone selections, an event's times are ambiguous and the timeline cannot reliably place the event. Every other capability here builds on these selections existing.

**Independent Test**: Can be fully tested by opening an event, selecting a start timezone and an end timezone, saving, then leaving and reopening the event to confirm both timezone selections were retained and applied to the event's times.

**Acceptance Scenarios**:

1. **Given** an event with a start time, **When** the traveler selects a start timezone and saves, **Then** the start timezone selection is stored and shown when the event is reopened.
2. **Given** an event with both a start and end time, **When** the traveler selects a start timezone and an end timezone and saves, **Then** both timezone selections are stored and applied to the corresponding times.
3. **Given** an event that has a start time, **When** the traveler attempts to save without a start timezone selected, **Then** the system prevents the save and asks the traveler to choose a start timezone.
4. **Given** an event with no end time, **When** the traveler saves it, **Then** no end timezone is required and the event saves successfully.

---

### User Story 2 - Edit Existing Event Fields (Priority: P1)

A traveler edits an event that already exists and changes any of its fields — the original fields (title, type, location, start/end times, color, leg) and the new fields (start timezone, end timezone, confirmation/reservation code, and notes) — and the changes are saved without disturbing the event's relationship to its trip leg or the timeline.

**Why this priority**: The new timezone selections and notes are only valuable if travelers can reliably edit events after creation. Plans change, times shift, and travelers need to correct event content at any time. This is required for the feature to deliver day-to-day value alongside User Story 1.

**Independent Test**: Can be fully tested by opening an existing event, changing several fields including the timezone selections, confirmation/reservation code, and notes, saving, and confirming the updated values persist and the event still belongs to the same leg and appears correctly on the timeline.

**Acceptance Scenarios**:

1. **Given** an existing event on a leg, **When** the traveler edits one or more of its fields and saves, **Then** the updated values are stored and the event remains related to the same trip leg.
2. **Given** an event shown on the timeline, **When** the traveler edits a field that affects its timeline placement (such as start/end times or the timezone selections) and saves, **Then** the timeline reflects the change.
3. **Given** the traveler is editing an event, **When** they make changes and then cancel before saving, **Then** none of the changes are applied and the event keeps its previous values.
4. **Given** the traveler edits a required field to an empty or invalid value, **When** they try to save, **Then** the system prevents the save and highlights what needs to be corrected.

---

### User Story 3 - Quick-Fill Start and End from the Trip Leg (Priority: P2)

A traveler uses a shortcut while creating or editing an event to copy the event's start and end from the trip leg it belongs to — rather than typing them by hand — so aligning an event to its leg's dates (and their timezones) is fast and consistent.

**Why this priority**: Most events start and end within their leg's dates, so re-entering those values by hand is repetitive and error-prone. A shortcut that copies the leg's start and end makes events fast to create, but it is an accelerator on top of the fields defined in User Stories 1 and 2, so it follows them.

**Independent Test**: Can be fully tested by creating an event on a leg, invoking the shortcut, and confirming the event's start and end (and their timezone selections) are populated from the leg, then saving and confirming the populated values persist.

**Acceptance Scenarios**:

1. **Given** the traveler is creating or editing an event on a leg, **When** they invoke the quick-fill shortcut, **Then** the event's start and end (and their timezone selections) are populated from the trip leg's start and end without further typing.
2. **Given** the traveler has already entered a start or end value, **When** they invoke the shortcut, **Then** the system either preserves their existing entry or asks before overwriting it, so no manual input is lost silently.
3. **Given** the shortcut has populated the start and end, **When** the traveler changes any populated value before saving, **Then** their manual change is kept and saved instead of the shortcut value.
4. **Given** the trip leg has no end value, **When** the traveler invokes the shortcut, **Then** the event's end is left unchanged and the traveler is not shown an error.

### Edge Cases

- An event's start and end timezone selections differ (the event crosses a timezone boundary within its leg).
- An event has a start time but the traveler leaves the start timezone unselected.
- The quick-fill shortcut is invoked when the event's start or end already contains manually entered data.
- The quick-fill shortcut is invoked on a leg that has no end value.
- A traveler enters a confirmation/reservation code longer than 255 characters.
- A traveler enters an unusually long value in the notes field and it must be stored and displayed without breaking the event view.
- A traveler edits an event and removes a value from a required field (such as clearing the title or the start timezone).
- An event's times, once combined with their selected timezones, fall outside the leg's timeframe.
- A traveler attempts to edit an event on a trip they do not own.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow a traveler to select a start timezone and an end timezone for an event, in addition to its existing fields.
- **FR-002**: The system MUST allow a traveler to record an optional Confirmation/Reservation Code on an event as a text input up to 255 characters.
- **FR-003**: The system MUST show the Confirmation/Reservation Code input before the Notes field in the event details modal.
- **FR-004**: The system MUST label the longer free-text field as Notes and allow a traveler to record optional notes on an event.
- **FR-005**: The system MUST require a start timezone selection when the event has a start time.
- **FR-006**: The system MUST require an end timezone selection when the event has an end time, and MUST NOT require one when the event has no end time.
- **FR-007**: The system MUST apply each selected timezone to the corresponding event time so the event's start and end are unambiguous.
- **FR-008**: The system MUST allow a traveler to edit any editable field on an existing event, including the original fields and the new start timezone, end timezone, confirmation/reservation code, and notes fields.
- **FR-009**: The system MUST persist edited field values and show them when the event is reopened.
- **FR-010**: The system MUST keep an edited event related to the same trip leg unless the traveler explicitly changes the leg.
- **FR-011**: The system MUST reflect edits to timeline-affecting fields (start/end times and the timezone selections) in the trip timeline.
- **FR-012**: The system MUST allow a traveler to clear the optional confirmation/reservation code and notes fields and save the event with those fields empty.
- **FR-013**: The system MUST discard all pending edits when a traveler cancels an edit before saving, leaving the event's prior values intact.
- **FR-014**: The system MUST prevent saving when a required field is empty or invalid and clearly indicate what must be corrected.
- **FR-015**: The system MUST provide a quick-fill shortcut that copies the event's start and end (and their timezone selections) from the event's trip leg.
- **FR-016**: The system MUST protect manually entered start and end values when the shortcut is invoked, either by preserving them or by confirming before overwriting.
- **FR-017**: The system MUST keep any manual change a traveler makes to a shortcut-populated value when the event is saved.
- **FR-018**: The system MUST leave a target value unchanged, without raising an error, when the trip leg has no corresponding value (for example, no end).
- **FR-019**: The system MUST restrict editing an event's fields and using the shortcut to the trip's owner.
- **FR-020**: The system MUST store and display notes up to a maximum length of 2,000 characters without truncating or corrupting the value, and MUST prevent saving notes that exceed that limit.

### Key Entities *(include if feature involves data)*

- **Event**: A trip occurrence (activity, reservation, reminder) related to a trip leg. In addition to its existing fields (type, title, location, start/end times, color, leg relationship), it now carries a start timezone selection, an end timezone selection (when it has an end), an optional confirmation/reservation code, and optional free-text notes.
- **Trip Leg**: The dated segment an event belongs to; also the source of the start and end values (and their timezones) used by the quick-fill shortcut.
- **Quick-Fill Shortcut**: A traveler-invoked action that copies the trip leg's start and end (and their timezone selections) into the event's start and end, without overwriting manual entries silently.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A traveler can add start and end timezone selections, a confirmation/reservation code, and notes to an event and save successfully on the first attempt without external guidance.
- **SC-002**: 100% of saved event field values, including timezone selections and notes, are retained and shown correctly when the event is reopened.
- **SC-003**: A traveler can align an event's start and end to its leg using the quick-fill shortcut at least 40% faster than entering the same values manually.
- **SC-004**: No manually entered start or end value is ever silently overwritten by the shortcut.
- **SC-005**: 100% of events with a start (or end) time are saved only after a corresponding timezone is selected.
- **SC-006**: A traveler can edit any event field and have timeline-affecting changes reflected on the timeline every time.

## Assumptions

- This feature extends the existing trip event (tracked item) defined in feature 007 rather than introducing a new kind of record; events continue to relate to exactly one trip leg.
- The existing event fields from feature 007 (type, title, location, start/end times, color, and leg relationship) remain, and this feature adds start timezone selection, end timezone selection, confirmation/reservation code, and notes on top of them.
- Title remains a required field; confirmation/reservation code and notes are optional; a start (or end) timezone is required only when the event has a start (or end) time.
- The quick-fill shortcut's source is the event's trip leg, copying the leg's start and end date/time and their timezone selections into the event, because that is the most repetitive timing information to enter.
- The available timezone choices follow the same set already established for trip legs in feature 006/007; this feature does not redefine how timezones are listed or resolved.
- When the shortcut copies the leg's start/end, it copies the leg's corresponding timezone selection so the copied times remain unambiguous.
- Confirmation/reservation code is captured as a 255-character text input before Notes, and Notes is captured as a single free-text field with a maximum length of 2,000 characters.
- Ownership and access control follow the existing model in which a trip and its events are private to the trip's owner.
