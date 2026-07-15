# Feature Specification: Email Event Ingestion

**Feature Branch**: `021-email-event-ingestion`

**Created**: 2026-07-15

**Status**: Draft

**Input**: User description: "need to have an email sent to an inbox, then a process to parse the email contents for potential usage in the system as event data."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Forward Confirmation Email to Trip Inbox (Priority: P1)

A traveler receives a booking confirmation email (flight, hotel, rental car, etc.) and forwards it to a dedicated trip inbox address. The system receives the email, parses the structured content (dates, times, locations, confirmation numbers), and creates one or more trip events populated with the extracted data.

**Why this priority**: This is the core value proposition — reducing manual data entry for common trip events by letting users forward emails they already receive.

**Independent Test**: Can be fully tested by forwarding a sample booking confirmation email to the designated inbox and verifying that a corresponding trip event is created with correct date, time, and location data.

**Acceptance Scenarios**:

1. **Given** a trip exists in the system, **When** a user forwards a flight confirmation email to the trip inbox, **Then** a flight event is created in the trip with the correct departure/arrival times, airports, and flight number.
2. **Given** a trip exists in the system, **When** a user forwards a hotel confirmation email to the trip inbox, **Then** a lodging event is created with the correct check-in/check-out dates and hotel name.
3. **Given** an email is forwarded to the inbox, **When** the system cannot confidently parse event data, **Then** the email is stored as an unprocessed item and the user is notified to review it manually.
4. **Given** an email is received with no clear trip association, **When** parsing completes, **Then** the user is prompted to assign the extracted event to a specific trip or leg.

---

### User Story 2 - Review and Confirm Parsed Events (Priority: P2)

After the system automatically parses an incoming email, the user reviews the extracted event data in the application, confirms or corrects the details, and saves the event to their trip timeline.

**Why this priority**: Automated parsing is imperfect. Users need a straightforward way to validate and accept (or reject) what was extracted before it lands on their timeline.

**Independent Test**: Can be fully tested by viewing the parsed-event review queue in the UI, editing a field, and confirming the event — verifying the timeline updates correctly.

**Acceptance Scenarios**:

1. **Given** an email has been parsed, **When** the user opens the review queue, **Then** they see the extracted event fields (type, date, time, location, reference number) pre-populated and editable.
2. **Given** the user reviews a parsed event, **When** they confirm it, **Then** the event is saved to the selected trip leg on the timeline.
3. **Given** the user reviews a parsed event, **When** they discard it, **Then** the email is marked as ignored and no event is created.
4. **Given** a parsed event has incorrect data, **When** the user edits and confirms it, **Then** the corrected event is saved and the original parse result is not retained.

---

### User Story 3 - View Inbox Processing History (Priority: P3)

A user can view a history of emails received by the trip inbox, including their processing status (parsed successfully, needs review, ignored), so they can audit what has been ingested and re-process items if needed.

**Why this priority**: Provides transparency and recovery for missed or failed parses without being required for the core ingestion flow.

**Independent Test**: Can be fully tested by listing the inbox history in the UI and verifying statuses reflect the outcome of prior processing runs.

**Acceptance Scenarios**:

1. **Given** emails have been received by the inbox, **When** the user opens the inbox history view, **Then** each email is shown with its subject, received date, and processing status.
2. **Given** an email was previously ignored or failed, **When** the user selects it and chooses to re-process, **Then** the system attempts parsing again and updates the status.

---

### Edge Cases

- What happens when the same confirmation email is forwarded more than once?
- How does the system handle emails containing multiple reservations (e.g., a combined flight + hotel booking)?
- How does the system handle non-English emails or international date/time formats?
- What happens when the inbox receives spam or non-travel emails?
- How does the system handle attachments (e.g., PDF itineraries) vs. plain-text or HTML email bodies?
- What happens if the extracted date falls outside the date range of any existing trip leg?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a dedicated email inbox address that can receive forwarded booking/confirmation emails.
- **FR-002**: The system MUST automatically process incoming emails and attempt to extract event data (event type, dates, times, locations, reference/confirmation numbers).
- **FR-003**: The system MUST support parsing of common travel confirmation formats including flights, hotels, rental cars, and activities.
- **FR-004**: The system MUST queue parsed results for user review before committing them to a trip timeline.
- **FR-005**: Users MUST be able to confirm, edit, or discard a parsed event from the review queue.
- **FR-006**: The system MUST associate a parsed event with a specific trip or trip leg, either automatically (when the trip context can be inferred from the email) or through user selection.
- **FR-007**: The system MUST detect and deduplicate emails that have already been processed to prevent duplicate events.
- **FR-008**: The system MUST store the raw email alongside the parsed result to support re-processing and audit.
- **FR-009**: The system MUST notify the user (via existing notification channels) when new parsed items are awaiting review.
- **FR-010**: The system MUST expose the inbox processing history with per-email status (parsed, pending review, confirmed, ignored, failed).
- **FR-011**: Users MUST be able to re-process a previously ignored or failed email.

### Key Entities

- **Inbox Email**: A raw email received by the trip inbox — includes sender, subject, body (plain-text and/or HTML), received timestamp, and processing status.
- **Parsed Event Draft**: The structured output from processing an inbox email — includes extracted event type, date/time, location, reference number, confidence level, source email reference, and review status (pending, confirmed, discarded).
- **Trip Event** (existing): The confirmed event entity that lives on a trip leg timeline; a Parsed Event Draft is promoted to a Trip Event upon user confirmation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can forward a booking confirmation email and see a reviewable parsed event in under 2 minutes of sending.
- **SC-002**: At least 80% of forwarded standard-format booking confirmations (flights, hotels, rental cars) produce a parseable draft with correct event type and dates without manual correction.
- **SC-003**: Users can complete the review-and-confirm flow for a parsed event in under 60 seconds.
- **SC-004**: Zero duplicate trip events are created from a single forwarded email, regardless of how many times the email is forwarded.
- **SC-005**: 100% of received emails are stored and accessible in the inbox history, regardless of parse outcome.

## Assumptions

- Each trip will have a unique inbox address (or users will reference their trip in the email subject/body); a mechanism to associate incoming emails to specific trips will be defined during planning.
- The system will initially parse email body text (plain-text and HTML); PDF attachment parsing is out of scope for v1.
- Parsing will focus on English-language emails in v1; international format support is a future enhancement.
- The existing Trip Event data model (event type, date/time, location, notes) is sufficient to represent parsed event data; no new event fields are required beyond what already exists.
- The existing notification infrastructure (feature 011) will be reused to alert users of new items in the review queue.
- Users must already have a trip created in the system before associating a parsed event; inbox emails cannot auto-create trips.
- Spam filtering and security scanning of inbound emails is handled by the email infrastructure provider, not by application code.
