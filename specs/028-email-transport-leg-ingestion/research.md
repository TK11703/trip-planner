# Research: Creating Trip Legs from Forwarded Transportation Bookings

**Feature**: 028-email-transport-leg-ingestion | **Date**: 2026-09-24

The spec's seven open decisions were closed in the Clarifications session of 2026-09-24 and are not revisited here. What remained was entirely technical: *where* each behaviour lives in a codebase that already has an ingestion pipeline, a leg model, and an eligibility rule that currently contradict each other. Every Technical Context unknown was resolved by reading the existing code; no external research was required, because the stack is fixed by the constitution and by features 021–025.

## Findings from the existing codebase

These are the facts the decisions below rest on. Several of them cut against what the spec's framing might suggest.

| Question | Answer | Source |
| -------- | ------ | ------ |
| Does recognition know anything about origin, destination, or mode? | No. The system prompt constrains `itemType` to `flight`\|`hotel`\|`car_rental`\|`activity`\|`other` and asks for a single `location`. There is no origin/destination concept anywhere in ingestion | `EmailParserService.cs` |
| Does anything classify a draft as leg-vs-item? | No. `ToDraft` always produces an item-shaped draft with `TripId: null, TripLegId: null` | `EmailParserService.cs` |
| Can the review queue create anything other than an item? | No. `ConfirmDraftEndpoint` is the only write path out of the queue and it builds a `CreateTrackedItemRequest` unconditionally | `ConfirmDraftEndpoint.cs` |
| Is there a leg-created notification already? | Yes — `ItineraryChangeKind.TripLegCreated`, rendered as "added a new leg to the trip" | `ItineraryNotificationService.cs:135` |
| Is there a leg-create audit operation already? | Yes — `AuditOperations.TripLegCreate` (`"trip-leg.create"`) | `AuditContracts.cs:9` |
| Does `TripLegValidator` enforce overlap? | No. It checks title, both zones, end ≥ start, the trip date range, and the travel shape. Nothing else | `TripLegValidator.cs` |
| Can `TripLegValidator` express "the end is missing"? | **No.** `ValidateCore` takes `DateTime endLocal` and `string endTimeZoneId` — non-nullable. Absence is unrepresentable in its signature | `TripLegValidator.cs` |
| Does `POST /inbox/{id}/reprocess` already re-run recognition? | Yes, but it **inserts new drafts**. Pointing FR-045 at it would duplicate the queue | `RelayMessageProcessor.ReprocessAsync` |
| Are restricted legs already excluded from the item leg picker? | Yes, in both places — the SQL (`GetPlacementCandidateLegs.sql`) and the modal (`detail.Legs.Where(l => l.CanContainItems)`) | feature 025 |
| Is the schema versioned? | **Yes.** Since feature 026 `DatabaseInitializer` applies each script in `Scripts/Schema/` exactly once under an advisory lock, recording id + checksum in `schema_migrations`, and blocks startup if an applied script was edited. Highest is `015`. Note the headers of `013`/`014` still claim otherwise — they predate 026 and are stale | `DatabaseInitializer.cs`, `Scripts/Initialization/migration_ledger.sql` |

The practical consequence: this is a **recognition and write-path feature**. The leg model, the validator, the eligibility triggers, and the notification and audit vocabularies are all already correct and are touched by nothing below. The contradiction the spec describes exists purely because ingestion has one write and the itinerary has two shapes.

---

## D1: Recognition reports facts; code decides the outcome

**Decision**: Extend the recognizer's JSON schema with `transportationMode`, `origin`, `destination`, `travelCost`, and `travelCostCurrency`, and extend `itemType` with `train`, `bus`, and `boat`. Do **not** ask the model whether the booking should become a leg. A new `DraftOutcomeClassifier` in the ingestion slice decides that deterministically from the returned fields, and a new `TransportationModeInterpreter` maps the model's vocabulary onto `TransportationModes`.

The classifier's whole rule:

```text
proposedOutcome = Leg  ⟺  TransportationModeInterpreter.Interpret(mode, itemType) is not null
```

`Interpret` returns one of the five supported modes or null. It accepts the exact mode names, the `itemType` values that imply one (`flight` → Flight, `car_rental` → Car), and a small fixed synonym table for what models actually emit: `ferry`/`cruise`/`ship` → Boat, `rail`/`train service` → Train, `coach`/`motorcoach` → Bus, `plane`/`air` → Flight, `rideshare`/`taxi`/`shuttle`/`rental car` → Car. Anything else — including the spec's "ferry described as a cruise or a rideshare" edge case falling outside the table — returns null and the draft goes down the existing item path untouched.

**Rationale**: FR-036 requires the car-rental outcome to be *consistent* — "the same booking does not become a leg one time and an item the next". A model asked to make a judgement call cannot promise that; a lookup table can. FR-005 requires a booking that cannot be confidently placed as transportation to fall through to the non-transport path rather than have a mode guessed, which is exactly "null means item". And the rule is unit-testable without a provider, which is what makes SC-002 checkable at all.

Extending `itemType` rather than replacing it keeps FR-006 satisfiable: a hotel or activity payload deserializes and classifies exactly as it does today, byte for byte. `ConfirmDraftEndpoint.NormalizeItemType` gains `train`/`bus`/`boat` alongside `flight`/`hotel`/`car_rental` in its map to `TrackedItemTypes.Reservation`, so a traveler who overrides a train draft to an item gets a Reservation, not an Event.

**Alternatives considered**:

- *Ask the model for a `proposedOutcome` field directly.* Rejected. It hands a product rule to a non-deterministic component and makes FR-036 unprovable.
- *Replace the `itemType` vocabulary with the five modes.* Rejected. `hotel` and `activity` have no mode, so the enum would need both vocabularies anyway, and every existing recognition test would change for no gain.
- *Infer transport from the presence of an origin alone.* Rejected. `TripLegShape.Resolve` already uses that inference for legacy leg rows, and reusing it here would make "the email mentioned a departure city" enough to reclassify a hotel booking.

## D2: The end time zone is filled by the traveler and by nobody else

**Decision**: Leave `FillMissingTimeZonesAsync` exactly as it is — Azure Maps fills a missing **start** zone from `location`, and nothing else. Do not extend the lookup to the end zone via `destination`. The only thing that ever puts a value in an empty end time zone is the traveler accepting the labelled suggestion described in D5.

**Rationale**: This is the single most tempting change in the feature and the one FR-034 exists to forbid. Resolving `Europe/London` from a recognized destination would make most transport drafts confirmable in one click — and it would fill "an end time zone the email did not state", which is precisely the sentence FR-034 prohibits, and would put legs into the itinerary carrying a zone the traveler never saw, which SC-006 counts as a defect. The start-zone lookup survives because FR-004 says recognition keeps extracting what it extracts today and FR-006 says the non-transport path is unchanged; widening it is new behaviour, not preserved behaviour.

The cost is real and accepted: transport emails frequently state no arrival zone, so US3's missing-detail path will be common rather than exceptional. The spec anticipates this ("This is not an edge case") and answers it with the one-click default rather than with inference.

**Alternatives considered**:

- *Resolve the end zone from `destination` through `IPlaceTimeZoneLookup`.* Rejected as above. Recorded here because it will be proposed again during implementation.
- *Default the end zone to the start zone at recognition time.* Rejected outright — FR-034 names this exact substitution as the thing that must not happen silently, and a flight is the canonical case where the two zones legitimately differ.

## D3: The proposed outcome is a persisted draft field, not a request parameter

**Decision**: Add `proposed_outcome` (`'leg'`\|`'item'`) to `parsed_item_drafts`, written by recognition and editable through the existing `PUT /drafts/{id}`. `POST /drafts/{id}/confirm` keeps its single route and branches on the **stored** value.

**Rationale**: FR-024 requires the entity created to be exactly the one the traveler selected at the moment of confirmation, and FR-008 requires the outcome to be recorded on the draft so the queue can present the right fields. One stored value satisfies both and gives the client and the server a single source of truth. It also inherits the sequencing `InboxDrafts.razor` already uses — save the traveler's choice, then confirm — which feature 024 established as "the confirm endpoint acts on the draft's saved state".

**Alternatives considered**:

- *A second route, `POST /drafts/{id}/confirm-as-leg`.* Rejected. It splits one traveler action across two routes and creates a state where the client's intent and the draft's stored outcome disagree, which is the ambiguity FR-024 is written against.
- *Pass the outcome in the confirm request body.* Rejected for the same reason, plus it would leave the stored outcome permanently unreliable for the traceability record FR-038 needs.
- *Derive the outcome at confirm time from whether an origin is present.* Rejected. It makes the traveler's override (FR-022) impossible to express — clearing the origin to force an item is not an interface.

## D4: Missing leg details are named by the endpoint, not by the validator

**Decision**: `TripLegValidator` is **not modified by this feature**. The new confirm-as-leg handler checks for the absence of each detail a leg requires — end date/time, end time zone, origin, destination, transportation mode — *before* it can construct a `CreateTripLegRequest`, and returns a field-named `400` for the first one missing. Once the request can be built, it goes through the same `TripLegValidator.Validate(CreateTripLegRequest, TripDetail)` call `TripLegEndpoints.CreateAsync` makes.

The *complete* list of missing details (FR-031, US3 scenario 4) is produced in the review screen by the leg-mode validation model, an `IValidatableObject` that yields one `ValidationResult` per missing field — the same mechanism `DraftEditModal.DraftModel` already uses to report several problems at once. Confirm stays disabled until that model passes. The API's single-field refusal is the backstop that guarantees nothing is written, not the primary reporting surface.

**Rationale**: The validator's signature takes `DateTime endLocal` and `string endTimeZoneId` — it cannot represent absence, so it cannot be the thing that reports absence. Changing it to nullables would ripple into both `TripLegEndpoints` write paths and the `TripLegForm`, putting hand-entered legs at risk to serve an email path. Keeping the validator untouched is what makes SC-003 ("legs created from email pass the same validation as hand-entered legs") true by construction rather than by testing.

The split also puts the message where the traveler can act on it: FR-031 asks for every fault reported "against the specific detail at fault, so the traveler can correct it in place", and in-place correction happens in the modal, not in an HTTP response.

**Alternatives considered**:

- *Extend `ApiError` to carry a list of offending fields.* Rejected for now. It is a cross-cutting change to the error contract used by every endpoint in the solution, for a benefit the review screen already delivers. Recorded as deferred; if a second caller ever needs it, it is a small change then.
- *Make the validator's end parameters nullable and let it report absence.* Rejected as above.
- *Let the modal be the only check.* Rejected. Client-side validation is a convenience; FR-032 requires that nothing is created when confirmation is refused, and only the API can promise that.

## D5: The labelled default is computed in the browser, offered as an inert suggestion, and applies to the end time zone only

**Decision**: One suggestion, rendered by a new `DraftSuggestionRow.razor` beneath the empty End timezone control:

> The email didn't state an arrival time zone. Use the departure zone (**America/Denver**)?  `[ Use this ]`

Until the button is pressed the field is empty, the missing-detail message is showing, and Confirm is disabled. Pressing it writes the value into the bound model field and marks that field traveler-supplied. The value is never computed on the server, never persisted by recognition, and never applied on the traveler's behalf — the confirm endpoint refuses a missing end zone identically whether the UI offered a suggestion or not.

No suggestion is offered for the missing **end date/time**. US3 scenario 1 requires the system to state that it is required and "not populate one on the traveler's behalf", and every candidate default is either an invention (a guessed journey duration) or nonsense (an arrival equal to the departure, which would pass `end >= start` and put a zero-length flight on the itinerary). FR-034's permission is a MAY, and the spec's own example is the time zone; this feature exercises it only there.

**Rationale**: FR-034 draws the line at *shown, labelled, and accepted*: "a value applied without being shown does not" satisfy FR-028. A component that renders the proposal as text next to an empty field, and changes nothing until a click, is that line made literal. Keeping the computation client-side means there is no server code path that could ever fill the field, which is a stronger guarantee than a server-side rule that happens not to fire.

The row takes `(field label, proposed value, why sentence, accept callback)`, so if a second default is ever sanctioned it is added as data rather than as new UI.

**Alternatives considered**:

- *Pre-fill the end zone and mark it "suggested" with a badge the traveler can clear.* Rejected. A pre-filled field is an applied value; the traveler who never scrolls to it has accepted nothing, and SC-006 counts that leg as a defect.
- *Compute the suggestion server-side and return it on the DTO.* Rejected. It adds a field to the contract that only says "the start zone", which the client already has, and it creates a server-side notion of a default that a future endpoint could accidentally apply.

## D6: Re-recognition is a traveler-triggered write, not a side effect of a read

**Decision**: A new `POST /api/email-ingestion/drafts/{id}/re-recognize`. The draft carries `transport_recognition_state` with three values:

| State | Meaning | Set by |
| ----- | ------- | ------ |
| `current` | Recognition has seen this draft with the transport schema | Insert default, for every draft created after this feature ships |
| `pending` | Recognized before this feature; transport fields were never asked for | The `016` script, once, for rows still `pending_review` |
| `unavailable` | Re-recognition was attempted and produced nothing usable | The endpoint, on failure |

`GET /drafts` surfaces the state and remains a pure read — no provider call, no added latency, no added cost to the queue, and feature 024's SC-008 ("placement computed during the existing request with no additional round trip") is preserved. `DraftEditModal.OnInitializedAsync` calls the endpoint once, and only when the state is `pending`; the card shows a brief "checking this booking for travel details" while it runs.

The endpoint re-runs `IItemRecognizer` over the stored message text — `AssembleText` moves out of `RelayMessageProcessor` into a shared `EmailTextAssembler` so both callers provably use the same assembly — and **merges into the existing draft row**. It must not use `RelayMessageProcessor.ReprocessAsync`, which inserts new drafts and would duplicate the queue.

An email that produced several drafts returns several recognized items, so one has to be chosen. In order: a case-insensitive `confirmation_code` match; failing that, the recognized item whose `startLocal` is nearest the draft's; failing that, the sole item when exactly one came back. If none of the three resolves, the state becomes `unavailable` and nothing is written.

Any failure — provider unavailable, nothing recognized, no confident match — sets `unavailable`, returns `200` with the draft unchanged, and leaves it fully confirmable on the item path (FR-047). The state moves off `pending` on the first attempt either way, so a broken provider cannot make the draft re-request on every open, and reopening a draft costs at most one provider call for its whole life.

**Rationale**: FR-045 says "the next time the traveler opens it", which is a read in today's design. Putting an Azure OpenAI round trip inside `GET /drafts` would charge every queue load for work that applies to a shrinking set of legacy rows, and would make the queue's latency depend on a third party. A traveler-initiated write, bounded to one call per draft and gated on a stored state, delivers the same observable behaviour at the moment the spec asks for it.

Re-recognition runs inside the request, like everything else in this pipeline. `NoMailboxMonitoringTests` still holds; no background service is introduced.

**Alternatives considered**:

- *Re-run inside `GET /drafts`.* Rejected — the cost and latency above, on every load, for every traveler.
- *Re-run inside `GET` but only for `pending` rows.* Rejected. It is the same round trip, merely rarer, and it makes a read endpoint perform a provider call and a write, which is the property that makes it hard to reason about.
- *Re-run everything in a one-off migration at deploy time.* Rejected. It is a provider call per pending draft with nobody watching, it spends money on drafts the traveler may discard, and it conflicts with the feature's stance that ingestion acts when the traveler acts.
- *Reuse `POST /inbox/{id}/reprocess`.* Rejected. It inserts new drafts; FR-042 requires the existing draft to survive with its edits intact, not to be shadowed by a duplicate.

## D7: A traveler's value is one they saved, tracked explicitly

**Decision**: Add `traveler_edited_fields text[] NOT NULL DEFAULT '{}'` to `parsed_item_drafts`. `UpdateDraftEndpoint` diffs the incoming request against the stored row and unions the names of the changed fields into the array. Re-recognition applies a value only when the target field is **both** currently absent **and** absent from that array.

**Rationale**: FR-046 forbids a re-recognized value overwriting "a value the traveler supplied by hand", and the obvious cheap rule — *only fill what is null* — gets that right for every field the traveler filled in but wrong for every field the traveler deliberately **cleared**. A traveler who deleted a wrong confirmation code and saved would have it restored on the next open, which is the same violation in the other direction. The array is what makes a cleared field stay cleared.

Computing the diff on the server means the client sends nothing new and no existing caller changes. The array is append-only within a draft's pending life and is never read by anything except the merge.

**Alternatives considered**:

- *Fill only absent fields.* Rejected — the cleared-field hole above. Kept as the fallback rule *inside* the array check, because both conditions must hold.
- *Snapshot the original recognized values in a `jsonb` column and treat any difference as a traveler edit.* Rejected. It stores a second copy of every field to answer a boolean question, and a recognized value the traveler happened to retype identically would read as unedited.
- *A single `traveler_edited_at_utc` timestamp.* Rejected. It is all-or-nothing: one edit to the title would freeze the whole draft against re-recognition, defeating FR-045's purpose of capturing origin, destination, and mode.

## D8: Switching the outcome changes one column and erases nothing

**Decision**: Toggling between Leg and Item writes `proposed_outcome` and nothing else. No transport field is cleared when the draft becomes an item; no item field is cleared when it becomes a leg. The review screen names what will not carry across at the moment of the switch:

- **Leg → Item**: origin, destination, transportation mode, travel cost — "these won't appear on the item".
- **Item → Leg**: item type, and the containing trip leg — "a leg isn't placed inside another leg".

Confirmation reads only the fields its outcome needs and silently ignores the rest.

**Rationale**: FR-025 requires the shared details to survive a switch in either direction and FR-026 requires the traveler to be told when something will not carry. Erasing on switch satisfies neither — SC-004 requires moving between outcomes "and back without losing any detail the two outcomes share", and a traveler who toggles to inspect the item fields and toggles back would find their origin gone. Keeping every column populated makes the round trip lossless by construction, and turns FR-026 into a message rather than a data-loss mitigation. The notice can then honestly say "won't appear on" rather than "will be deleted".

**Alternatives considered**:

- *Clear the fields the new outcome cannot use.* Rejected as above.
- *Move origin into the item's `location` on a Leg → Item switch.* Rejected. It is a transformation the traveler did not ask for, and FR-028's prohibition on substituted values applies to both outcomes.

## D9: Travel cost carries the amount; the currency is shown, never stored on the leg

**Decision**: Recognize `travelCost` as a number and `travelCostCurrency` as a string. Persist both on the draft. Write **only** the amount to `CreateTripLegRequest.TravelCost`. The currency is used solely to label the review field — "Email stated: EUR 240.00" — and never reaches `trip_legs`.

**Rationale**: The clarification settled that the numeric amount is captured regardless of the currency stated, and multi-currency conversion is out of scope. But FR-010 also says "the traveler reviews the amount before confirming", and the spec's edge case is explicit that a mismatched currency is something "the traveler has to notice this during review". They cannot notice what is not shown. One nullable text column with exactly one consumer is the smallest thing that makes the review honest; it introduces no behaviour, no conversion, and no change to the leg model.

**Alternatives considered**:

- *Drop the currency entirely.* Rejected. It makes the edge case undetectable and leaves the traveler reviewing a bare number.
- *Add a currency to `trip_legs`.* Rejected outright. The leg model is out of scope (spec Assumptions, Out of Scope), and multi-currency is a product decision this feature has no mandate to make.

## D10: Nothing already confirmed is touched

**Decision**: The `016` script contains no `INSERT`, `UPDATE`, or `DELETE` against `tracked_items` or `trip_legs`. `tracked_items` is not named at all; `trip_legs` appears only in the `created_trip_leg_id` foreign-key clause, which FR-040 requires. Its only non-additive statement is setting `transport_recognition_state = 'pending'` on `parsed_item_drafts` rows that are still `pending_review` — a value no traveler has ever seen, on rows that have created nothing.

**Rationale**: FR-043 and FR-048 forbid automatic conversion, movement, or deletion of any already-confirmed item, and forbid offering a conversion action. The strongest way to guarantee that is for the migration to be incapable of it. Stated here explicitly because a "backfill existing flight reservations into legs" task is the natural thing to propose when reading the Context section, and it is exactly what the clarification session ruled out: the traveler creates the leg and deletes the item themselves.

FR-044 follows for free — pre-existing unassigned items are not referenced by anything this feature adds, so the timeline's unassigned lane behaves identically.
