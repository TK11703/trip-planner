# Feature Specification: Matching Ingested Email Items to Trip Legs

**Feature Branch**: `024-email-item-leg-matching`

**Created**: 2026-08-05

**Status**: Draft

**Input**: User description: "when a user receives emails about their trip and want to provide that content back to the system, we use a specific inbox to accept those emails from the users. Then the logic app monitors that inbox and snags a new email. The email details are submitted to the API for ingestion. Sometimes the email details might not align to the trip legs currently in a trip. Or a trip leg might not be available yet for the time frame of the email detail."

## Context

Feature 022 established the ingestion path: a monitored mailbox, an external relay that detects new mail and submits its content, and a review queue of parsed drafts. This feature addresses what happens **after** recognition — deciding which trip and which trip leg an item belongs to, and what the traveler does when the answer is "none of them."

Today a draft can only be confirmed if it already carries both a trip and a leg, and the review screen offers no way to supply either. A draft whose dates fall between legs, or that arrives before the relevant leg has been created, has no path to the timeline.

## Clarifications

### Session 2026-08-05

- Q: When exactly one editable leg contains the draft's timeframe, should the system apply that placement automatically? → A: Pre-select the trip and leg on the draft, but never write to the timeline without an explicit Confirm from the traveler.
- Q: What resolution is offered when no leg covers the item's timeframe? → A: Let the traveler confirm the item onto the trip with no leg. It lands in the timeline's unassigned area and can be related to a leg later, once one exists.
- Q: What happens when a traveler assigns an item to a leg whose window does not contain it? → A: Refuse, exactly as the item form does. Widening the leg is not offered.

Together these make a leg **optional** for an item but its travel window **binding** whenever a leg is assigned. That relaxation applies to manual entry as well, so both paths continue to agree.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A Forwarded Confirmation Lands on the Right Leg (Priority: P1)

A traveler forwards a hotel confirmation for the third night of a trip. The system recognizes the reservation and, from its dates, identifies which trip and which leg of that trip the stay falls inside. The traveler opens the review queue, sees the proposed placement, and confirms. The stay appears on the timeline against that leg.

**Why this priority**: This is the common case and the whole point of ingestion. Without automatic placement the traveler has to re-enter everything the email already said, which removes the reason to forward it at all.

**Independent Test**: Can be fully tested by ingesting a confirmation whose dates fall inside exactly one existing leg, opening the review queue, and verifying the proposed trip and leg are correct and that confirming produces a timeline item on that leg with the recognized details.

**Acceptance Scenarios**:

1. **Given** a traveler has a trip whose leg covers 12–15 August, **When** a draft is recognized with a start on 13 August, **Then** the review queue shows that draft with that trip and that leg already selected and Confirm available.
2. **Given** a draft with a correct pre-selected placement, **When** the traveler confirms it, **Then** an item is created on that leg carrying the recognized type, title, location, dates, time zones, and confirmation code.
3. **Given** a draft with a pre-selected placement, **When** the traveler never confirms it, **Then** no timeline item exists and the trip is unchanged.
4. **Given** a confirmed draft, **When** the traveler returns to the review queue, **Then** that draft is no longer pending.
5. **Given** a draft whose dates fall inside legs on two different trips the traveler can edit, **When** they open the review queue, **Then** both candidates are offered and neither is pre-selected.
6. **Given** a draft with no recognized start date/time, **When** the traveler opens it, **Then** the system reports that a start is required before placement can be resolved.

---

### User Story 2 - No Leg Covers the Item's Timeframe (Priority: P2)

A traveler forwards a car rental for a day that no leg currently covers — either the trip has a gap between legs, or the leg for that stretch of the trip has not been created yet. Rather than blocking, the system lets them confirm the booking onto the trip without a leg. It waits in the timeline's unassigned area, visible and safe, until the leg exists and the traveler relates it.

**Why this priority**: This is the failure the traveler actually reported. Ingestion runs ahead of planning — bookings get made and forwarded before the itinerary is filled in. Forcing the itinerary to be complete before a confirmation can be captured inverts the real order of events, and the drafts pile up unconfirmable.

**Independent Test**: Can be fully tested by ingesting a confirmation dated outside every leg of the traveler's only trip, confirming it without a leg, and verifying it appears in the timeline's unassigned area with its dates intact and the draft cleared from the queue.

**Acceptance Scenarios**:

1. **Given** a draft on a trip whose legs do not cover its dates, **When** the traveler opens it, **Then** the system states that no leg covers that timeframe and identifies the dates in question.
2. **Given** such a draft, **When** the traveler confirms it without choosing a leg, **Then** an item is created on the trip with no leg and the draft leaves the pending queue.
3. **Given** an item confirmed without a leg, **When** the traveler views the trip timeline, **Then** it appears in the unassigned area showing its dates and is visibly awaiting a leg.
4. **Given** an unassigned item and a newly created leg that covers its dates, **When** the traveler relates the item to that leg, **Then** it moves onto the leg and out of the unassigned area.
5. **Given** a draft on a trip that has no legs at all, **When** the traveler opens it, **Then** the same path applies rather than a generic failure.
6. **Given** a draft whose dates fall entirely outside its trip's own start and end dates, **When** the traveler opens it, **Then** the system distinguishes this from an ordinary gap between legs, because the trip itself would have to change.
7. **Given** a traveler neither confirms nor discards, **When** they leave the review queue, **Then** the draft stays pending and unchanged and no trip data is altered.

---

### User Story 3 - Correcting a Wrong Placement Before Confirming (Priority: P3)

The recognized dates or the proposed trip are wrong — the email described a return flight the system read as an outbound, or the traveler has two overlapping trips. The traveler overrides the proposal, picking the trip and leg themselves or correcting the dates, then confirms.

**Why this priority**: Recognition from free-form email is imperfect and placement inherits that imperfection. Override keeps the traveler in control, but it only matters once placement is being proposed at all.

**Independent Test**: Can be fully tested by ingesting a draft, changing its trip and leg away from the proposal, confirming, and verifying the item lands where the traveler chose rather than where the system suggested.

**Acceptance Scenarios**:

1. **Given** a draft with a proposed placement, **When** the traveler selects a different trip, **Then** the leg choices update to that trip's legs and any prior leg choice is cleared.
2. **Given** a draft with wrong recognized dates, **When** the traveler corrects them, **Then** the proposed placement is re-evaluated against the corrected dates.
3. **Given** a traveler overrides the proposal, **When** they confirm, **Then** the item is created against their chosen leg, not the proposed one.
4. **Given** a traveler can only view — not edit — a trip, **When** they choose placements, **Then** that trip is not offered.
5. **Given** the chosen leg is deleted or its dates change between proposal and confirmation, **When** the traveler confirms, **Then** the confirmation is refused with an explanation rather than placing the item against stale information.

---

### User Story 4 - Email Items Obey the Same Rules as Typed Items (Priority: P4)

An item that arrives by email is subject to exactly the same placement rules as one the traveler types into the item form. Assigning an item to a leg whose travel window does not contain it is refused on both paths, and confirming without a leg is permitted on both. There is no route by which forwarding an email produces an item the traveler could not have created by hand.

**Why this priority**: Two sets of rules for the same data eventually disagree, and the itinerary becomes untrustworthy. This is correctness insurance rather than new capability, so it ranks last — but it must be true before the feature ships.

**Independent Test**: Can be fully tested by attempting, through the email path, each placement the item form refuses, and verifying the email path refuses it too with a comparable explanation.

**Acceptance Scenarios**:

1. **Given** a traveler assigns a draft to a leg whose window does not contain the draft's dates, **When** they confirm, **Then** confirmation is refused and the system names the date that falls outside the leg.
2. **Given** the same out-of-window pairing entered through the item form, **When** the traveler saves, **Then** it is refused for the same reason and in comparable wording.
3. **Given** a draft confirmed successfully, **When** the resulting item is opened in the item form, **Then** it passes validation without the traveler changing anything.
4. **Given** a draft missing a detail the item form requires, **When** the traveler confirms, **Then** the system names the missing detail rather than substituting a placeholder that the traveler did not choose.
5. **Given** an item is created from a draft, **When** collaborators on that trip are notified, **Then** the notification is the same kind they receive for a manually added item.

---

### Edge Cases

- A draft's timeframe spans two consecutive legs — which leg wins, and is the traveler told the item crosses a boundary?
- Two legs of the same trip overlap in time, so more than one leg is a valid container.
- The item's start falls inside a leg but its end falls past that leg's end, so the pairing is refused even though the start looked right.
- The recognized start and end carry different time zones from the leg's own start and end zones, so "inside the window" must be judged as instants rather than wall-clock text.
- A draft has a start but no end.
- The relay delivers a message for a traveler who has no trips at all, so there is nothing to confirm onto.
- The traveler's edit access to the target trip is revoked between ingestion and confirmation.
- The same reservation is forwarded twice and both drafts are confirmed to the same leg.
- A leg's dates are edited after placement, leaving an already-confirmed item outside its leg's window.
- An unassigned item is never related to a leg and the trip is completed with it still parked.
- A leg is created that covers an unassigned item's dates, but the traveler is not told the item is now placeable.
- The trip has many legs, so the placement choice needs to stay usable rather than presenting a long undifferentiated list.

## Requirements *(mandatory)*

### Functional Requirements

**Placement proposal**

- **FR-001**: System MUST evaluate each recognized draft against the traveler's trips and legs and determine which leg, if any, contains the draft's timeframe.
- **FR-002**: System MUST judge containment by comparing the draft's start and end as points in time against the leg's own travel window, honoring the time zones recorded on both.
- **FR-003**: System MUST consider only trips the traveler is permitted to modify.
- **FR-004**: System MUST pre-select the trip and leg on a draft when exactly one qualifying leg is found, so the traveler's only remaining action is to confirm.
- **FR-005**: System MUST present every candidate when more than one leg qualifies, and pre-select none of them.
- **FR-006**: System MUST NOT create, alter, or delete any trip, leg, or timeline item as a result of evaluating placement. Confirmation MUST remain an explicit traveler action in every case.
- **FR-007**: System MUST show why a placement was pre-selected — which trip, which leg, and the dates that matched.
- **FR-008**: System MUST re-evaluate placement whenever the traveler changes the draft's dates or its trip.

**Uncovered timeframes**

- **FR-009**: System MUST detect when a draft's timeframe is covered by no leg of the selected trip and report that distinctly from other confirmation failures.
- **FR-010**: System MUST distinguish a gap between or around existing legs from a draft that falls outside the trip's own date range.
- **FR-011**: Users MUST be able to confirm a draft onto a trip without selecting a leg.
- **FR-012**: System MUST show an item that has no leg in the trip timeline's unassigned area, with its dates, identifiable as awaiting a leg.
- **FR-013**: Users MUST be able to relate an unassigned item to a leg later, subject to the same travel-window rule as any other assignment.
- **FR-014**: System MUST handle a trip with no legs at all through this same path rather than a generic error.
- **FR-015**: System MUST leave the draft pending and the trip unchanged when the traveler neither confirms nor discards.

**Traveler override**

- **FR-016**: Users MUST be able to choose the trip and the leg for a draft themselves, overriding any pre-selection.
- **FR-017**: Users MUST be able to correct a draft's recognized dates, times, and time zones before confirming.
- **FR-018**: System MUST clear a leg selection that no longer belongs to the selected trip.
- **FR-019**: System MUST refuse confirmation and explain why when the chosen leg no longer exists or no longer covers the item's timeframe at the moment of confirmation.

**Consistency with manual entry**

- **FR-020**: System MUST refuse to place an item on a leg whose travel window does not contain the item's timeframe, whether the item comes from a draft or from the item form. Widening the leg MUST NOT be offered as part of that refusal.
- **FR-021**: System MUST treat a leg as optional on both paths, so that an item created without a leg through email review is equally creatable through the item form.
- **FR-022**: System MUST report a rejected confirmation against the specific detail at fault so the traveler can correct it in place.
- **FR-023**: System MUST NOT substitute invented values for details the item form would require the traveler to supply.
- **FR-024**: System MUST produce the same collaborator notifications for an item created from a draft as for one added by hand.

**Auditability**

- **FR-025**: System MUST record, for each confirmed draft, which item it became and which leg it landed on — or that it landed unassigned — so a traveler can trace a timeline entry back to the email it came from.
- **FR-026**: System MUST retain a pending draft's state across sessions until the traveler confirms or discards it.

### Key Entities

- **Parsed Item Draft**: A recognized reservation or event awaiting review. Carries the recognized details, a review state, and — once resolved — the trip and leg it is destined for. Exists independently of the timeline until confirmed.
- **Placement Proposal**: The system's assessment of where a draft belongs. Identifies zero, one, or several candidate legs and the reason each qualified. Advisory, not persisted itinerary data.
- **Coverage Gap**: The condition where a draft's timeframe falls outside every leg of its trip. Distinguishes a gap inside the trip's date range from one outside it, because the second implies the trip itself is wrong.
- **Trip Leg**: The existing container that gives an item its travel window. Its start and end, with their time zones, define what "covered" means. Optional for an item, but binding once assigned.
- **Tracked Item**: The timeline entry a confirmed draft becomes. Indistinguishable from a manually created item once written. May be assigned to a leg or unassigned.
- **Unassigned Item**: A tracked item belonging to a trip but not to any leg. A holding state for bookings that arrived before the itinerary caught up, surfaced in the timeline so it is not forgotten.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a forwarded confirmation whose dates fall inside exactly one existing leg, the traveler reaches a placed timeline item with a single confirming action and no data entry.
- **SC-002**: 100% of drafts whose timeframe is covered by exactly one editable leg have that trip and leg pre-selected without traveler input.
- **SC-003**: No draft can reach a state where it is neither confirmable nor explicable — every pending draft either has a placement, offers confirmation without a leg, or displays the specific reason it can do neither.
- **SC-004**: A traveler whose booking falls outside every leg can capture it against the trip without leaving the review queue and without altering the itinerary.
- **SC-005**: Every unassigned item is visible on its trip's timeline and can be related to a covering leg once one exists.
- **SC-006**: Zero items exist whose dates fall outside the travel window of the leg they are assigned to, verified by re-validating every item created through the email path.
- **SC-007**: Every timeline item created from an email can be traced back to the message it came from.
- **SC-008**: Placement pre-selection is available when the traveler opens the review queue, with no perceptible wait attributable to matching.
- **SC-009**: Existing ingestion behavior established in feature 022 — recognition, duplicate suppression, and sender-to-traveler matching — is unchanged.

## Assumptions

- The mailbox, the external relay, and the recognition step established in feature 022 are unchanged by this feature; it begins at the point where a draft exists.
- Placement is decided by the item's timeframe. Location, title, and confirmation code may inform display but are not required to make a match, since a leg's defining attribute is its travel window.
- A traveler reviews their own drafts. Drafts are not shared with trip collaborators for review.
- The review queue is the single place placement is resolved. Bulk resolution across many drafts at once is out of scope.
- Time zones recorded on drafts and legs are trustworthy inputs; this feature does not attempt to infer or correct a missing time zone beyond what recognition already provides.
- Where the feature must choose between altering the traveler's itinerary and asking, it asks. Ingested content is a suggestion until the traveler accepts it.
- The trip's own date range remains authoritative — this feature does not silently widen a trip or a leg to accommodate an email.
- Making a leg optional is a relaxation of an existing rule that applies to items generally, not a special case for imported ones. Both entry paths change together.

## Dependencies

- Feature 021 / 022: email ingestion, parsed drafts, and the review queue.
- Existing trip leg travel windows and the validation rules already applied to manually created items.
- Existing trip sharing permissions, which determine the trips a traveler may place an item into.

## Out of Scope

- Changing how email reaches the system, how it is parsed, or how duplicates are suppressed.
- Creating or editing trip legs from the review queue.
- Automatically creating trips from email.
- Reconciling items that were already confirmed before this feature existed.
- Re-placing items when a leg's dates are edited after the fact.
