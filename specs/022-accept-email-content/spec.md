# Feature Specification: Relayed Email Content Ingestion

**Feature Branch**: `[022-accept-email-content]`

**Created**: 2026-07-31

**Status**: Draft

**Input**: User description: "the system should be able to accept email content from a user and try to process it within a trip."

**Refined**: "A logic app is going to be established, which will monitor a mailbox. Once that logic app detects a new email, it will take the email body and possibly attachments and make a call to one of the api endpoints to ingest it. The API should not have a background service to monitor an email box. So we need to remove anything that resembles this."

## Clarifications

### Session 2026-07-31

- Q: How does email content reach the system? → A: An external automation relay (Logic App) monitors the mailbox and calls an API ingestion endpoint with the message content.
- Q: Should the API monitor a mailbox or poll stored messages for later processing? → A: No. Existing behavior of this kind must be removed.
- Q: Are attachments included? → A: Yes, the ingestion call may include attachments.
- Q: How is a message associated with a traveler? → A: By the sender address of the original email, matched to a known traveler, because the relay authenticates as itself rather than as the traveler.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Relayed Email Becomes a Reviewable Draft (Priority: P1)

A traveler sends or forwards a travel confirmation email to the monitored mailbox. An external automation relay detects the message and hands its content — subject, body, and any attachments — to the system. The system processes that content during the call and records any recognized itinerary events as drafts awaiting the traveler's review.

**Why this priority**: This is the entire ingestion path. Without a relay-driven entry point that processes content on receipt, no email content can enter the system at all.

**Independent Test**: Can be fully tested by having the relay submit a flight confirmation for a known traveler and verifying that the response reports the outcome and that a reviewable draft exists for that traveler, with no trip timeline change.

**Acceptance Scenarios**:

1. **Given** an authorized relay submits a message whose sender matches a known traveler, **When** the message contains one recognizable reservation, **Then** the system processes it during the call and records a draft for that traveler.
2. **Given** an authorized relay submits a message, **When** processing finishes, **Then** the response reports the ingestion outcome so the relay can distinguish success, no-usable-content, duplicate, and failure without polling.
3. **Given** a submitted message includes attachments, **When** the system processes it, **Then** the attachments are accepted and retained with the message, and any readable text they contain is available to the recognition step.
4. **Given** a submitted message's sender does not match a known traveler, **When** the system processes it, **Then** the message is rejected with a distinct outcome and no draft or trip change is made.
5. **Given** no recognizable itinerary information is found, **When** processing finishes, **Then** the outcome reports that no usable content was found and no draft is created.
6. **Given** an unauthenticated or unauthorized caller submits a message, **When** the request is received, **Then** it is rejected and nothing is stored.

---

### User Story 2 - Review and Confirm Recognized Events (Priority: P2)

A traveler reviews the drafts produced from their relayed email, corrects wrong or missing details, chooses the trip and leg the event belongs to, and confirms which drafts become real events on the trip timeline.

**Why this priority**: Recognition from free-form email is imperfect. Review keeps the traveler in control and protects itinerary accuracy, but it only has value once content can be ingested.

**Independent Test**: Can be fully tested by opening the review queue for a traveler with an existing draft, editing a field, assigning a trip leg, confirming it, and verifying the event appears on the timeline with the corrected values.

**Acceptance Scenarios**:

1. **Given** drafts exist for a traveler, **When** they open the review queue, **Then** each draft shows its recognized event type, title, dates, times, time zones, location, confirmation code, and notes.
2. **Given** a draft has wrong or missing details, **When** the traveler edits and confirms it, **Then** the corrected values are saved to the chosen trip leg.
3. **Given** a draft has no trip or leg assigned, **When** the traveler attempts to confirm it, **Then** confirmation is refused until a valid trip and leg are chosen.
4. **Given** a draft is discarded or never confirmed, **When** the traveler views the trip timeline, **Then** no event was created from it.
5. **Given** a traveler attempts to act on a draft that is not theirs, or on a trip they cannot modify, **When** they attempt it, **Then** the action is refused.

---

### User Story 3 - Avoid Reprocessing the Same Message (Priority: P3)

A traveler who forwards the same confirmation twice, or whose relay delivers the same message more than once, does not accumulate repeated drafts for the same reservation.

**Why this priority**: Relays retry and can redeliver. Without repeat protection the review queue fills with duplicates, which erodes trust, but this only matters once ingestion and review work.

**Independent Test**: Can be fully tested by submitting the identical message twice and verifying the second call reports a duplicate outcome and adds no second draft.

**Acceptance Scenarios**:

1. **Given** a message was already ingested, **When** the identical message is submitted again, **Then** the system reports a duplicate outcome and creates no additional draft.
2. **Given** a relay retries after a network failure with the same message, **When** the retry is processed, **Then** the traveler ends up with exactly one draft set for that message.
3. **Given** two genuinely different messages share a sender and subject, **When** both are submitted, **Then** both are processed as distinct messages.

### Edge Cases

- The body contains quoted replies, signatures, disclaimers, or marketing content alongside itinerary details.
- A single message contains several reservations, or restates the same reservation more than once.
- Dates or times are ambiguous, omit a year, or use an unfamiliar regional format.
- The message, or an attachment, is very large, empty, or contains no readable text.
- An attachment is a binary format whose text cannot be read.
- The recognition step is slow, unavailable, or returns unusable output.
- The relay sends malformed content or omits required fields.
- The sender address matches more than one traveler, or matches none.
- The same message is delivered twice concurrently.
- Content contains payment or identity information that must not be copied into event fields.

## Requirements *(mandatory)*

### Functional Requirements

#### Ingestion

- **FR-001**: The system MUST expose an ingestion endpoint that accepts the content of a single email message from an external automation relay.
- **FR-002**: The ingestion request MUST accept the sender address, recipient address, subject, received timestamp, message body, an originating message identifier, and zero or more attachments.
- **FR-003**: The system MUST accept and retain attachments supplied with a message, including file name, content type, and content.
- **FR-004**: The system MUST use readable text from the message body, and from attachments whose text can be read, as input to the recognition step.
- **FR-005**: The system MUST process a submitted message during the ingestion request and MUST NOT defer processing to a later background pass.
- **FR-006**: The system MUST return an outcome that distinguishes at least: drafts recognized, no usable content found, duplicate message, unknown sender, and processing failure.
- **FR-007**: The system MUST accept ingestion requests only from an authorized automation relay identity and MUST reject all other callers.
- **FR-008**: The system MUST determine the owning traveler from the sender address of the relayed message and MUST NOT attribute the message to the relay's own identity.
- **FR-009**: The system MUST reject a message whose sender cannot be matched to exactly one known traveler, without creating drafts.
- **FR-010**: The system MUST reject a request that is missing required content or exceeds the accepted size limit, and MUST report the reason.

#### Recognition and Review

- **FR-011**: For each recognized itinerary event, the system MUST create a draft holding the available event type, title, start and end date/time, time zones, location, confirmation code, and notes.
- **FR-012**: The system MUST NOT create, change, or remove any trip event as a direct result of ingestion.
- **FR-013**: Travelers MUST be able to view, edit, confirm, and discard their own drafts.
- **FR-014**: The system MUST require a valid trip and trip leg on a draft before it can be confirmed.
- **FR-015**: On confirmation, the system MUST create a trip event on the chosen leg using the reviewed values and mark the draft as confirmed.
- **FR-016**: The system MUST restrict every draft action to the traveler who owns the draft and MUST enforce existing trip modification permissions.
- **FR-017**: The system MUST notify a traveler through existing notification channels when new drafts are awaiting review.
- **FR-018**: The system MUST avoid copying payment card details, full identity-document numbers, or authentication credentials into draft event fields.

#### Repeat Delivery

- **FR-019**: The system MUST recognize a repeated delivery of the same message and MUST NOT create additional drafts for it.
- **FR-020**: Repeat detection MUST use the originating message identifier when supplied and MUST fall back to message characteristics when it is absent.

#### Removal of Mailbox Monitoring

- **FR-021**: The system MUST NOT contain any component that monitors, polls, or connects to a mailbox to retrieve messages.
- **FR-022**: The system MUST NOT contain a background process that scans stored messages for deferred processing.
- **FR-023**: Existing mailbox-monitoring and deferred-processing behavior MUST be removed, along with the storage structures, configuration, and entry points that exist only to support it.
- **FR-024**: Removing this behavior MUST NOT break a traveler's ability to view, edit, confirm, or discard drafts, or to view previously ingested messages.

### Key Entities *(include if feature involves data)*

- **Ingested Message**: A single email delivered by the relay — sender, recipient, subject, body, originating message identifier, received timestamp, owning traveler, and processing outcome.
- **Message Attachment**: A file delivered with an ingested message — file name, content type, size, and content.
- **Recognized Event Draft**: A proposed event derived from an ingested message, with recognized values, assigned trip and leg, and review state.
- **Trip Event**: An existing timeline item, created only when a traveler confirms a draft.
- **Relay Identity**: The authorized automation caller permitted to submit messages; never treated as the owning traveler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A message delivered to the monitored mailbox produces a reviewable draft, or a reported non-success outcome, within 2 minutes of delivery in at least 95% of cases.
- **SC-002**: The ingestion call returns a conclusive outcome — never "pending later processing" — in 100% of accepted requests.
- **SC-003**: For standard flight, lodging, rental car, and activity confirmations, at least 80% produce drafts with the correct event type and start date.
- **SC-004**: Messages from unknown senders and unauthorized callers create zero drafts and zero trip events in 100% of tests.
- **SC-005**: Submitting the same message twice produces exactly one draft set in 100% of tests.
- **SC-006**: Ingestion creates zero trip timeline events before traveler confirmation in 100% of tests.
- **SC-007**: 100% of confirmed drafts appear on the selected trip leg with the values the traveler approved.
- **SC-008**: The running system contains zero mailbox-monitoring or message-polling components after this feature ships.

## Assumptions

- The automation relay (Logic App) is provisioned and operated outside this codebase. This feature owns only the endpoint contract it calls and the processing behind it.
- The relay authenticates as an application identity, not as the traveler, so traveler attribution must come from the message's sender address.
- Travelers already have an email address recorded on their profile; that address is what matches a relayed message to a traveler.
- The relay is responsible for mailbox connection, spam filtering, retry, and dead-lettering of messages the system rejects.
- Attachments are accepted and retained in this version. Text extraction covers text-based attachment content; extracting text from binary formats such as PDF or images is out of scope.
- Recognition targets common English-language travel confirmations; other languages may yield partial or no results.
- Existing trip, leg, event validation, ownership, sharing, and accessibility rules continue to apply unchanged.
- The existing draft review queue, message history view, and notification channels are retained. This feature replaces how content arrives, not how it is reviewed.
