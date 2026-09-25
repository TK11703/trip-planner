# Feature Specification: Creating Trip Legs from Forwarded Transportation Bookings

**Feature Branch**: `028-email-transport-leg-ingestion`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "When a traveler forwards a transportation booking email (flight, train, bus, boat, or car rental) to the monitored ingestion mailbox, the system should be able to create a trip leg from it, not just a tracked item. Today every recognized booking becomes a tracked item, which is wrong for transportation bookings and actively broken given the feature 025 eligibility rules."

## Context

Three features meet here, and the last one broke the first.

Feature 021/022 established ingestion: a monitored mailbox, a relay that submits new mail, recognition that produces **parsed drafts**, and a review queue. Feature 024 decided where a confirmed draft lands — which trip, which leg, or unassigned — and established that nothing is written without an explicit confirmation and that no detail is invented on the traveler's behalf (FR-023). Feature 025 then split legs into **Stay** and **Travel**, gave Travel legs a transportation mode, and ruled that only a Stay leg or a **Car** travel leg may contain items. A passenger on a flight, train, bus, or boat cannot schedule their own stops, so those legs hold no items at all.

Ingestion never learned about that split. Recognition sorts a booking into a small set of categories — flight, hotel, car rental, activity, or other — and confirmation collapses the first three onto a single **reservation** item. Every recognized booking becomes an item, because creating an item is the only write the review queue can perform.

The result is a direct contradiction. A forwarded flight confirmation becomes a reservation item that **cannot be attached to the flight leg it describes** — the eligibility rule rejects it. It can only sit unassigned under the trip, or be misfiled under a stay or car leg it has nothing to do with. Meanwhile the flight leg that should have been created from that email either does not exist, or has to be typed in by hand while the item generated from the same email lingers beside it. The traveler forwarded an email describing a journey and got back something the itinerary model has no place for.

The leg model already describes transportation properly: a Travel leg carries an origin, a destination, a transportation mode, a travel cost, and a confirmation code. What ingestion cannot do is recognize that a booking **is** a journey, extract where it goes from and to, and create that leg.

Two gaps make this more than a mapping exercise:

- **A leg requires an end date/time and an end time zone; an item does not.** Recognition frequently returns neither. A draft that would confirm cleanly as an item may be unable to become a leg without the traveler supplying details the email did not state.
- **A car rental is genuinely two things.** A rental car is a means of travel — a `car` travel leg, the one travel mode that still accepts items. It is also a booking with a pickup counter, a reservation number, and a price, which is a reservation item. Both readings are defensible; this spec proposes the leg and lets the traveler override it to an item.

## Clarifications

### Session 2026-09-24

- Q: May a confirmed leg's window overlap an existing leg on the same trip — permitted, warned about, or refused? (FR-021) → A: Permitted and not warned about, matching hand-entered legs, which are subject to no overlap rule today. This feature adds none.
- Q: Does confirming a draft as a leg notify collaborators, and does a leg-created notification exist? (User Story 4, scenario 5) → A: It exists. Creating a leg already notifies trip collaborators, so a leg created from a draft uses that same existing notification. No new notification is added.
- Q: What does a recognized car rental become — a Car travel leg, a reservation item, or an undefaulted choice? (FR-035) → A: A Car travel leg. Car is the one travel mode that still accepts items, so the leg captures the route without giving up the ability to hold stops along the way, and rental confirmations usually state both a pickup and a return. The traveler may override it to an item.
- Q: May the review screen offer a labelled default for a missing end date/time or end time zone, or must every value be entered from scratch? (FR-034) → A: It may offer a clearly labelled default, such as an end time zone matching the start, that the traveler accepts in one action. A default that is shown, labelled, and accepted by the traveler is a suggestion rather than an invented value.
- Q: How are drafts already pending in the queue that describe transportation treated? (FR-045) → A: Recognition is re-run on the draft the next time the traveler opens it, so origin, destination, and mode can be captured and it can be proposed as a leg.
- Q: Is a conversion or guided remedy offered for items already confirmed from transportation emails? (User Story 5, scenario 4) → A: No. The traveler creates the leg by hand and deletes the item. No conversion action is added.
- Q: Is a recognized price captured as travel cost, and what if its currency differs from the trip's? (FR-010) → A: The numeric amount is always captured as the leg's travel cost regardless of the currency stated in the email. The traveler reviews it before confirming and may correct or clear it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A Forwarded Flight Becomes a Flight Leg (Priority: P1)

A traveler forwards an airline confirmation for a flight from Denver to London. The system recognizes it as transportation rather than a booking that happens during a stay, reads the origin, the destination, the departure and arrival times and their time zones, and the confirmation number. The review queue shows it as a **proposed trip leg**, not a proposed item. The traveler confirms, and a Flight travel leg appears on the trip carrying the route, the schedule, and the booking reference.

**Why this priority**: This is the broken case. Today this email produces a reservation item that cannot be attached to the leg it describes, so the traveler must hand-enter the leg anyway and then clean up the orphaned item. Nothing else in this feature matters if a flight still cannot become a flight.

**Independent Test**: Forward a flight confirmation naming both airports and both times, open the review queue, verify the draft is presented as a proposed travel leg with the mode, origin, destination, schedule, and confirmation number filled in from the email, confirm it, and verify a Flight leg exists on the trip with those values and that no tracked item was created from that draft.

**Acceptance Scenarios**:

1. **Given** a forwarded flight confirmation naming a departure and an arrival airport, **When** recognition completes, **Then** the draft is classified as transportation with mode Flight and carries an origin and a destination taken from the email.
2. **Given** such a draft, **When** the traveler opens the review queue, **Then** it is presented as a proposed trip leg and the review screen shows the leg details — mode, origin, destination, start, end, time zones, confirmation number — rather than item-only fields.
3. **Given** a transport draft with every detail a leg requires, **When** the traveler confirms it, **Then** a Travel leg is created on the chosen trip with that mode, route, schedule, time zones, and confirmation number, and no tracked item is created from that draft.
4. **Given** a confirmed transport draft, **When** the traveler returns to the review queue, **Then** that draft is no longer pending and records that it became a leg.
5. **Given** a forwarded train, bus, or boat confirmation, **When** recognition completes, **Then** the same path applies with the corresponding mode.
6. **Given** a forwarded hotel or activity confirmation, **When** recognition completes, **Then** it is **not** classified as transportation and continues through the existing item path unchanged.
7. **Given** a created leg, **When** the traveler opens it in the leg form, **Then** it passes validation without the traveler changing anything, exactly as a hand-entered leg would.

---

### User Story 2 - The Traveler Decides Leg or Item (Priority: P1)

Recognition proposes leg or item, but the traveler decides. A draft proposed as a leg can be turned into an item, and a draft proposed as an item can be turned into a leg, before confirming. A misclassification is recoverable in both directions, without discarding the draft and re-forwarding the email.

**Why this priority**: Recognition from free-form email is imperfect, and this classification now determines which **kind of entity** gets created — a far more consequential mistake than a wrong title. An airport shuttle booking, a ferry crossing that is really a day excursion, a hotel confirmation that is actually a sleeper train: each is arguable. Without a two-way override, a misclassified draft is unfixable from the review queue, which is the same dead end feature 024 set out to remove. It shares P1 with User Story 1 because shipping automatic classification without an override would replace one trap with another.

**Independent Test**: Take a draft recognized as transportation and switch it to an item; take a draft recognized as a non-transport booking and switch it to a leg. In both directions verify that the review screen presents the fields the chosen outcome requires, that confirmation creates that entity, and that the draft records which one it became.

**Acceptance Scenarios**:

1. **Given** a draft proposed as a trip leg, **When** the traveler switches it to an item, **Then** the review screen presents the item fields — including item type and leg placement — and confirming creates a tracked item under the existing rules.
2. **Given** a draft proposed as an item, **When** the traveler switches it to a trip leg, **Then** the review screen presents the leg fields — including transportation mode, origin, and destination — and confirming creates a leg.
3. **Given** a draft switched from item to leg, **When** details the leg requires are absent, **Then** the system names each missing detail and lets the traveler supply it before confirming.
4. **Given** a draft switched from leg to item, **When** the traveler confirms, **Then** the leg-only details that have no home on an item are not silently discarded without the traveler being told where they went.
5. **Given** a draft being reviewed as a leg, **When** the traveler selects a transportation mode other than the recognized one, **Then** the selected mode is used on confirmation.
6. **Given** a draft being reviewed as a leg, **When** the traveler changes the trip selection, **Then** the leg is created on the trip they chose.
7. **Given** any override, **When** the traveler leaves the review queue without confirming, **Then** the draft stays pending with their edits preserved and no trip data is altered.

---

### User Story 3 - A Booking the Email Did Not Fully Describe (Priority: P2)

A forwarded train ticket states a departure city and time but no arrival time, and no time zone for the arrival. A leg requires both. Rather than guessing an arrival or silently reusing the departure zone, the system tells the traveler exactly which details are missing and lets them supply them in the review screen. Nothing is written until they do.

**Why this priority**: This is not an edge case — recognition often returns no end at all, and an end date/time plus an end time zone are mandatory on every leg while both are optional on an item. Without this, a large share of transport drafts would be unconfirmable as legs and the traveler would be pushed back to hand entry. It ranks behind the first two because it is a completion rule for a path those stories establish.

**Independent Test**: Forward a transport confirmation that states no arrival time, open the draft as a leg, verify the missing end date/time and end time zone are named specifically and not pre-filled with a guess, supply them, confirm, and verify the leg is created with the supplied values.

**Acceptance Scenarios**:

1. **Given** a transport draft with no recognized end date/time, **When** the traveler reviews it as a leg, **Then** the system states that an end date/time is required and does not populate one on the traveler's behalf.
2. **Given** a transport draft with no recognized end time zone, **When** the traveler reviews it as a leg, **Then** the system states that an end time zone is required and does not substitute the start zone without the traveler choosing it.
3. **Given** a transport draft with no recognized origin, **When** the traveler reviews it as a leg, **Then** the system states that an origin is required, because a travel leg cannot exist without one.
4. **Given** a transport draft missing any detail a leg requires, **When** the traveler attempts to confirm it as a leg, **Then** confirmation is refused, every missing detail is named, and no leg, item, or other trip data is created or changed.
5. **Given** a refusal, **When** the traveler supplies the named details and confirms again, **Then** the leg is created.
6. **Given** a transport draft missing details a leg requires but not details an item requires, **When** the traveler switches it to an item, **Then** it can be confirmed as an item under the existing rules.
7. **Given** a recognized value the traveler disagrees with, **When** they correct it in the review screen, **Then** the corrected value is used and the recognized one is not reinstated.

---

### User Story 4 - Tracing a Leg Back to the Email It Came From (Priority: P2)

Weeks later a traveler looks at a flight leg on their itinerary and wants to know where its confirmation number came from. The leg traces back to the forwarded email, exactly as an item created from a draft already does.

**Why this priority**: Feature 024 (FR-025) made every email-created item traceable. A leg created the same way with no trace would be a visible regression in a property the product already guarantees. It is not required to create a correct leg, which is why it follows the creation stories.

**Independent Test**: Confirm a transport draft as a leg, then verify from the draft record which leg it became and from the leg which email it came from — with the same fidelity available today for a draft that became an item.

**Acceptance Scenarios**:

1. **Given** a draft confirmed as a leg, **When** the draft record is inspected, **Then** it identifies both that the outcome was a leg and which specific leg was created.
2. **Given** a draft confirmed as an item, **When** the draft record is inspected, **Then** it identifies the item it became, unchanged from today's behavior.
3. **Given** a leg created from a draft, **When** the traveler asks where it came from, **Then** the originating email is identifiable.
4. **Given** a leg created from a draft is later deleted, **When** the draft record is inspected, **Then** it still reflects that the draft was confirmed and does not reappear as pending.
5. **Given** a draft confirmed as a leg, **When** the leg is created, **Then** trip collaborators receive the same leg-created notification they already receive for a hand-entered leg, and no additional notification is introduced by this feature.

---

### User Story 5 - Bookings Already in the Queue and on the Timeline (Priority: P3)

A traveler has drafts that were recognized before this feature and flight reservations they already confirmed as items, some parked unassigned because no eligible leg would take them. They need to know what happens to both.

**Why this priority**: The contradiction described in the Context has already produced this data; the queue and the timeline contain it today. It ranks last because the feature is correct and shippable for new mail without touching history, but leaving the outcome undefined would leave travelers with stranded reservations and no stated remedy.

**Independent Test**: With pending drafts recognized before the change and confirmed reservation items that describe transportation, apply the change and verify each is handled as specified, with no draft becoming unreviewable and no existing item altered or deleted without the traveler's action.

**Acceptance Scenarios**:

1. **Given** a draft that was pending before this feature, **When** the traveler opens the review queue afterwards, **Then** it is still reviewable and confirmable, with no loss of its recognized details or its edits.
2. **Given** a pending pre-existing draft that describes transportation, **When** the traveler opens it, **Then** recognition is re-run so the booking's origin, destination, and mode can be captured, and the draft is offered as a proposed leg — without overwriting any edit the traveler had already made.
3. **Given** a reservation item already confirmed from a flight, train, bus, or boat email, **When** this feature ships, **Then** it is not moved, converted, or deleted automatically.
4. **Given** such an already-confirmed item, **When** the traveler wants it to be a leg instead, **Then** no conversion action is offered; they create the leg and delete the item themselves.
5. **Given** a pre-existing unassigned item that describes transportation, **When** the traveler views the timeline, **Then** it remains visible and usable under the existing unassigned-item rules.

### Edge Cases

- A single email describes a multi-leg journey — an outbound and a return, or a flight with a connection. Does it become one leg, several, or one per recognized segment?
- A booking email covers both transportation and a stay, such as a flight-plus-hotel package, so one message should produce both a leg and an item.
- A transport booking's window overlaps an existing leg on the same trip, so confirming it would create two legs claiming the same hours.
- A transport booking's window sits outside the trip's own start and end dates, which would require the trip itself to change.
- The same confirmation is forwarded twice and both drafts are confirmed, producing two identical legs.
- A transport draft is confirmed as a leg on a trip that has items sitting unassigned in that window — those items become placeable, or arguably belong on the new leg, but the system does not move them.
- Recognition reads the origin and destination backwards, producing a leg that travels the wrong way.
- Recognition returns an origin and a destination that are the same place, such as a round-trip ticket described as a single booking.
- The recognized start and end carry different time zones, which is normal for a flight and must be preserved rather than collapsed to one zone.
- A transport draft has a start but no trip selected, so there is no trip to create the leg on.
- The traveler's edit access to the target trip is revoked between recognition and confirmation.
- The traveler selects a trip whose legs are already complete and consecutive, leaving no room for the new leg.
- A draft is reviewed as a leg, confirmed, and the leg is deleted, while the draft still records it as confirmed.
- Recognition returns a transportation mode the leg model does not support, such as a ferry described as a cruise or a rideshare.
- A confirmation number recognized from the email exceeds what a leg accepts, or arrives blank after trimming.
- A recognized price is stated in a currency other than the one the traveler thinks in, so the amount captured as travel cost is numerically right but means something different — the traveler has to notice this during review (FR-010).
- A travel cost is recognized in a currency other than the trip's, or is not recognized at all.
- Two travelers share a trip and each forwards the same booking, producing two drafts for one journey.

## Requirements *(mandatory)*

### Functional Requirements

**Recognizing transportation**

- **FR-001**: System MUST distinguish a booking that describes transportation between two places from one that describes a stay, an activity, or another booking that happens at a single place.
- **FR-002**: System MUST determine a transportation mode for a recognized transportation booking from the set the leg model supports: Flight, Train, Bus, Boat, or Car.
- **FR-003**: System MUST extract an origin and a destination for a recognized transportation booking, and MUST record that either is absent rather than substituting a value.
- **FR-004**: System MUST continue to extract the start, end, time zones, confirmation code, and notes it extracts today, and MUST preserve differing start and end time zones rather than collapsing them.
- **FR-005**: System MUST classify a booking it cannot confidently place as transportation through the existing non-transport path rather than guessing a mode.
- **FR-006**: System MUST leave recognition of stays, activities, and other non-transportation bookings unchanged.
- **FR-007**: System MUST apply the existing confidence threshold, draft limits, and sensitive-value redaction to transportation bookings without exception.
- **FR-008**: System MUST record on each draft whether it is proposed as a trip leg or as a tracked item, so the review queue can present the right outcome.
- **FR-009**: System MUST NOT create a leg, an item, or any other trip data as a result of recognition. Confirmation MUST remain an explicit traveler action.
- **FR-010**: System MUST extract a travel cost when the booking states one, and MUST capture the numeric amount as the proposed leg's travel cost regardless of the currency the email states. The traveler reviews the amount before confirming and may correct or clear it.

**Reviewing and confirming a transport draft**

- **FR-011**: System MUST present a draft proposed as a leg with the details a leg requires — transportation mode, origin, destination, start, end, both time zones, title, confirmation code, and notes — populated from recognition where available.
- **FR-012**: Users MUST be able to correct any recognized leg detail before confirming, including the transportation mode, the origin, and the destination.
- **FR-013**: Users MUST be able to choose the trip a leg will be created on, limited to trips they are permitted to modify.
- **FR-014**: System MUST create a trip leg on confirmation of a draft reviewed as a leg, carrying the reviewed values, and MUST NOT also create a tracked item from that draft.
- **FR-015**: System MUST apply the same validation to a leg created from a draft as to one entered by hand, so no leg exists that the traveler could not have created themselves.
- **FR-016**: System MUST leave a draft pending, with the traveler's edits intact and the trip unchanged, when the traveler neither confirms nor discards it.
- **FR-017**: Users MUST be able to discard a transport draft without creating anything, as they can today.
- **FR-018**: System MUST refuse confirmation and explain why when the selected trip no longer exists, or the traveler's permission to edit it has been withdrawn, rather than writing against stale information.
- **FR-019**: System MUST NOT modify, move, or delete existing legs or items as a side effect of creating a leg from a draft.
- **FR-020**: System MUST record an audit entry for a leg created from a draft equivalent to the one recorded for an item created from a draft.
- **FR-021**: System MUST permit a leg whose confirmed window overlaps an existing leg on the same trip, and MUST NOT refuse or warn on overlap alone, matching the treatment of a hand-entered leg.

**Overriding the leg-or-item choice**

- **FR-022**: Users MUST be able to change a draft proposed as a leg into an item, and a draft proposed as an item into a leg, before confirming.
- **FR-023**: System MUST present the fields required by whichever outcome is currently selected, and MUST re-evaluate which details are missing when the selection changes.
- **FR-024**: System MUST create exactly the entity the traveler selected at the moment of confirmation, regardless of what recognition proposed.
- **FR-025**: System MUST preserve details common to both outcomes — title, start, end, time zones, confirmation code, notes, location — across a switch in either direction.
- **FR-026**: System MUST tell the traveler when details will not carry across a switch, rather than dropping them silently.
- **FR-027**: System MUST continue to exclude Flight, Train, Bus, and Boat legs from the leg choices offered for a draft being reviewed as an item, as required by feature 025.

**Missing details**

- **FR-028**: System MUST NOT substitute invented values for details a leg requires, consistent with FR-023 of feature 024.
- **FR-029**: System MUST require an end date/time and an end time zone before a draft can be confirmed as a leg, and MUST name each one specifically when it is absent.
- **FR-030**: System MUST require a non-blank origin before a draft can be confirmed as a travel leg, and MUST name it specifically when it is absent.
- **FR-031**: System MUST report every missing or invalid leg detail against the specific detail at fault, so the traveler can correct it in place.
- **FR-032**: System MUST NOT create or change any trip data when confirmation is refused for missing details.
- **FR-033**: System MUST allow a draft that cannot be completed as a leg to be confirmed as an item instead, when it satisfies the item rules.
- **FR-034**: System MUST NOT silently fill an end time zone or an end date/time the email did not state. System MAY offer a clearly labelled default — such as an end time zone matching the start — that the traveler accepts in a single action. A default that is shown, labelled as a suggestion, and accepted by the traveler satisfies FR-028; a value applied without being shown does not.

**Car rental**

- **FR-035**: System MUST propose a recognized car rental as a Car travel leg, using the pickup location as the origin and the return location as the destination.
- **FR-036**: System MUST apply the car rental outcome consistently, so the same booking does not become a leg one time and an item the next.
- **FR-037**: Users MUST be able to override the car rental outcome in both directions, under FR-022.

**Traceability**

- **FR-038**: System MUST record, for each confirmed draft, which kind of entity it became and which specific entity, extending FR-025 of feature 024 to cover legs.
- **FR-039**: System MUST allow a leg created from a draft to be traced back to the email it came from.
- **FR-040**: System MUST keep a confirmed draft out of the pending queue even if the entity it created is later deleted.
- **FR-041**: System MUST preserve the existing draft-to-item trace for drafts confirmed as items, unchanged.

**Existing data**

- **FR-042**: System MUST keep every draft pending before this feature reviewable and confirmable afterwards, with its recognized details and traveler edits intact.
- **FR-043**: System MUST NOT automatically convert, move, or delete any tracked item that was already confirmed, including reservation items created from transportation emails.
- **FR-044**: System MUST keep pre-existing unassigned items visible and usable under the existing unassigned-item rules.
- **FR-045**: System MUST re-run recognition on a pre-existing pending draft the first time the traveler opens it after this feature ships, so origin, destination, and transportation mode can be captured and the draft can be proposed as a leg.
- **FR-046**: System MUST preserve any edit the traveler already made to a pre-existing draft when recognition is re-run. A re-recognized value MUST NOT overwrite a value the traveler supplied by hand.
- **FR-047**: System MUST leave a pre-existing draft reviewable and confirmable on the item path when re-running recognition fails or recognizes nothing, rather than blocking the draft or discarding it.
- **FR-048**: System MUST NOT offer any automatic conversion of a tracked item that was already confirmed from a transportation email. A traveler who wants such an item to be a leg creates the leg and deletes the item themselves.

### Key Entities

- **Parsed Item Draft**: A recognized booking awaiting review. Already carries the recognized details, a review state, and a destination trip and leg. Gains a proposed outcome — leg or item — and, for transportation, a mode, an origin, and a destination. Still creates nothing until confirmed.
- **Proposed Outcome**: Whether a draft is destined to become a trip leg or a tracked item. Proposed by recognition, decided by the traveler, and binding at the moment of confirmation.
- **Transportation Booking**: A recognized booking that moves the traveler between two places. Distinguished from a stay or an activity by having an origin and a destination rather than a single location.
- **Trip Leg**: The existing dated segment of a trip. A Travel leg carries a transportation mode, an origin, a destination, a start and end with their time zones, and optional travel cost and confirmation code. It requires both an end and an end time zone — the constraint that makes a leg harder to create from an email than an item.
- **Transportation Mode**: Flight, Train, Bus, Boat, or Car. Determines what the leg means and, under feature 025, whether it may contain items. Car is the only mode that may.
- **Tracked Item**: The timeline entry a non-transport draft becomes, unchanged by this feature. Cannot be attached to a Flight, Train, Bus, or Boat leg.
- **Draft Outcome Record**: What a confirmed draft became — which kind of entity and which one — so an itinerary entry can be traced back to the email that produced it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A forwarded flight, train, bus, or boat confirmation reaches a correctly described travel leg on the trip with a single confirming action when the email states everything a leg requires.
- **SC-002**: Zero drafts recognized as flight, train, bus, or boat transportation are confirmed into tracked items without the traveler explicitly choosing that outcome.
- **SC-003**: 100% of legs created from email pass the same validation as hand-entered legs when reopened in the leg form, with no traveler correction required.
- **SC-004**: Every pending draft can be moved between the leg outcome and the item outcome and back without discarding it, and without losing any detail the two outcomes share.
- **SC-005**: No draft reaches a state where it can be neither confirmed nor explained — every pending draft either confirms, offers the alternate outcome, or names the specific details it is missing.
- **SC-006**: Zero legs are created carrying an end date/time, end time zone, or origin the traveler did not see and accept.
- **SC-007**: Every trip leg created from an email can be traced back to the message it came from, matching the traceability already provided for email-created items.
- **SC-008**: 100% of drafts pending before this feature remain reviewable and confirmable afterwards, and zero already-confirmed items are altered or deleted without traveler action.
- **SC-009**: Recognition and confirmation of hotel, activity, and other non-transportation bookings are unchanged, verified by re-running existing ingestion scenarios.
- **SC-010**: Ingestion and the leg eligibility model no longer contradict each other: no email-created entity exists that the itinerary model has no valid place for.

## Assumptions

- Recognition, the monitored mailbox, the relay, duplicate suppression, and sender-to-traveler matching established in features 021 and 022 are otherwise unchanged; this feature changes what recognition classifies and what confirmation may create.
- The leg model from feature 025 is correct and is not revisited. Flight, Train, Bus, Boat, and Car remain the complete mode set, and only Stay and Car legs may contain items.
- The review queue remains the single place a draft is resolved. Bulk resolution across many drafts is out of scope, as in feature 024.
- A traveler reviews their own drafts; drafts are not shared with collaborators for review.
- A leg is created on a trip the traveler selects. This feature does not create trips from email.
- Where the feature must choose between changing the traveler's itinerary and asking, it asks. Ingested content remains a suggestion until accepted.
- Origin and destination recognized from an email are free text describing places, matching how the leg form already accepts them; no geocoding or airport-code resolution is assumed.
- A leg's travel window is authoritative once created; this feature does not widen a trip or an adjacent leg to accommodate a booking.
- Existing trip permissions, audit behavior, and time-zone handling apply unchanged to legs created from email.

## Dependencies

- Features 021 and 022: the monitored mailbox, the relay, recognition, and the review queue.
- Feature 024: draft placement, the optional-leg rule for items, the no-invented-values principle, and draft-to-item traceability.
- Feature 025: leg classification, transportation modes, and the item-eligibility rules enforced in the database.
- The existing trip leg creation and validation rules, including the mandatory end date/time, end time zone, and travel-leg origin.
- Existing trip sharing permissions, which determine the trips a traveler may create a leg on.

## Out of Scope

- Changing how email reaches the system, how duplicates are suppressed, or how a sender is matched to a traveler.
- Changing the leg model, the transportation mode set, or the item-eligibility rules established by feature 025.
- Creating trips automatically from email.
- Splitting one email into multiple legs for a multi-segment journey, unless resolved otherwise by the multi-leg edge case.
- Capturing carrier, flight number, train service, vessel, seat, terminal, gate, or platform details, which feature 025 placed out of scope for legs generally.
- Automatically reassigning existing unassigned items onto a leg created from email.
- Bulk review or bulk confirmation of drafts.
- Multi-currency conversion for travel cost.
