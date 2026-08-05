# Research: Matching Ingested Email Items to Trip Legs

**Feature**: 024-email-item-leg-matching | **Date**: 2026-08-05

All Technical Context unknowns were resolved by reading the existing code. No external research was required — the stack, storage, and testing approach are fixed by the constitution and by features 021–023.

## Findings from the existing codebase

These facts shaped every decision below and are worth stating plainly, because several of them contradict what the spec's framing might suggest.

| Question | Answer | Source |
| -------- | ------ | ------ |
| Does anything match drafts to trips today? | No. `EmailParserService` sets `TripId: null, TripLegId: null` and `RelayMessageProcessor` inserts the draft unchanged | `EmailParserService.cs`, `RelayMessageProcessor.cs` |
| Can a draft's fields be edited via the API? | Yes — `PUT /drafts/{id}` already accepts all eleven fields. It is simply unused by the UI | `UpdateDraftEndpoint.cs` |
| Does the Web client expose that? | Yes — `IEmailIngestionApiClient.UpdateDraftAsync` exists and is called from nowhere | `EmailIngestionApiClient.cs` |
| Is `tracked_items.trip_leg_id` nullable? | Yes, already, with `ON DELETE SET NULL` | `005_trip_leg_items.sql` |
| Does the timeline render item without a leg? | Yes — an "Unassigned / Needs a trip leg" lane already exists | `TripTimeline.razor` |
| Does confirmation validate? | No. `ConfirmDraftEndpoint` calls `CreateTrackedItemAsync` directly, bypassing `TrackedItemValidator` | `ConfirmDraftEndpoint.cs` |
| How are accessible trips resolved? | Owner rows `UNION ALL` share rows matched by `member_user_id` or lowercased `member_email` | `GetTripsPage.sql` |
| Which access levels can edit? | `TripAccessLevel.Owner` and `.Collaborator`; `.Viewer` is read-only | `TripContracts.cs` |

The practical consequence: this feature is **mostly a UI and validation feature**, not a data feature. One nullable column is the entire schema change.

---

## D1: Where placement is computed

**Decision**: Compute placement at read time, inside the existing draft-list request. Never persist a suggestion.

**Rationale**: FR-006 forbids altering data as a result of evaluating placement, and FR-008 requires re-evaluation whenever the draft's dates or trip change. A persisted guess violates the first and complicates the second — it would also go stale the moment a traveler edits a leg's dates, producing a suggestion that no longer matches reality. Computing per request makes both requirements fall out for free, and the data volume (tens of drafts, a handful of legs) makes the cost irrelevant.

The stored `trip_id` and `trip_leg_id` on `parsed_item_drafts` therefore mean exactly one thing: *the traveler's explicit choice*. They are null until the traveler saves an assignment.

**Alternatives considered**:

- *Write the match into the draft at ingestion time.* Rejected. It conflates a guess with a decision, goes stale, and would require re-running matching on every leg edit.
- *Match in a background job.* Rejected outright — feature 022 forbids background services in the API, and `NoMailboxMonitoringTests` actively guards against reintroducing one.
- *Match in the browser.* Rejected. It would ship every trip's legs to the client and duplicate the window logic that already lives in `TrackedItemValidator`.

## D2: Making a trip leg optional

**Decision**: Change `CreateTrackedItemRequest.TripLegId` and `UpdateTrackedItemRequest.TripLegId` from `Guid` to `Guid?`. In `TrackedItemValidator`, drop the "trip has no legs" and "leg is required" rejections; keep and strengthen the window check so it runs only when a leg is present.

**Rationale**: Q2 of the clarification session put an item on a trip with no leg, and Q3 kept the window binding. Those two answers describe exactly this rule shape. The database already permits it and the timeline already renders it, so the requirement was only ever enforced in the validator.

Using `Guid?` rather than continuing to overload `Guid.Empty` matters: `Guid.Empty` cannot distinguish "the traveler chose no leg" from "the field was never populated", and that distinction is the whole point of FR-011.

**Alternatives considered**:

- *Keep `Guid` and treat `Guid.Empty` as unassigned.* Rejected. It is a sentinel that every call site must remember to check, and it makes the JSON contract lie about intent.
- *Add a separate `IsUnassigned` flag.* Rejected as redundant state that can disagree with the id.
- *Allow unassigned only for email-created items.* Rejected — it is precisely the two-sets-of-rules problem US4 exists to prevent, and FR-021 forbids it.

## D3: Validating confirmation

**Decision**: `ConfirmDraftEndpoint` loads the `TripDetail`, builds a `CreateTrackedItemRequest`, and runs `TrackedItemValidator` before calling the repository. A failure returns a validation problem naming the offending field.

**Rationale**: FR-020 requires it, and the current bypass is a live correctness bug — today the email path can write an item whose dates fall outside its leg. Reusing the validator rather than reimplementing the checks is what keeps the two paths from drifting.

**Alternatives considered**:

- *Have the confirm endpoint call the item-creation endpoint over HTTP.* Rejected — an internal HTTP hop for no benefit.
- *Validate only in the Blazor form.* Rejected. Client-side validation is a convenience, not a guarantee; the API is the boundary.

## D4: Deriving the placement result

**Decision**: Model the outcome as a small value object returned with each draft:

- `SuggestedTripId` / `SuggestedTripLegId` — populated only when exactly one candidate exists
- `Candidates` — every qualifying trip/leg pair, with the leg's window for display
- `Status` — one of `Matched`, `Ambiguous`, `NoLegCovers`, `OutsideTripDates`, `InsufficientData`

**Rationale**: The five statuses map one-to-one onto the messages the spec requires. `NoLegCovers` versus `OutsideTripDates` satisfies FR-010's demand that a gap between legs read differently from a draft that falls outside the trip entirely. `InsufficientData` covers the no-start-date case in US1 scenario 6. Returning the status rather than letting the UI infer it from empty collections keeps the messaging logic in one place.

**Alternatives considered**:

- *Return only a nullable suggested leg id.* Rejected — the UI could not tell "nothing covers this" from "several things do", and those need opposite messages.
- *Return a numeric confidence.* Rejected. Containment is boolean; a score would imply a precision that does not exist.

## D5: Comparing instants, not wall clocks

**Decision**: Reuse the `ToInstant(DateTime local, TimeZoneInfo zone)` conversion already in `TrackedItemValidator` — specify `DateTimeKind.Unspecified`, take the zone's offset for that local time, and convert to UTC. Compare the resulting `DateTimeOffset` values.

**Rationale**: FR-002 requires it, and a red-eye landing at 06:00 local in a different zone from the leg's is the ordinary case, not an exotic one. The existing helper is already correct; duplicating the logic in the matcher would invite the two copies to disagree.

Implementation note: the helper should move to a shared location so the matcher and the validator provably share it, rather than being copy-pasted.

**Alternatives considered**:

- *Compare `DateTime` values directly.* Rejected — silently wrong across zones, and the bug would appear only for travelers crossing them.
- *Store everything in UTC on the draft.* Rejected. Drafts intentionally keep local time plus zone id so the traveler sees what the email said.

## D6: Traceability

**Decision**: Add `tracked_item_id uuid NULL REFERENCES tracked_items(tracked_item_id) ON DELETE SET NULL` to `parsed_item_drafts` in a new idempotent script `013_draft_item_traceability.sql`, written on confirmation.

**Rationale**: FR-025 requires tracing a timeline entry back to its email, and no such link exists — `parsed_item_drafts` records the source `inbox_email_id` but nothing about the item it became. `ON DELETE SET NULL` matches the pattern `005_trip_leg_items.sql` established, so deleting an item does not cascade into ingestion history.

**Alternatives considered**:

- *Store the draft id on the item instead.* Rejected. It puts ingestion concerns on the core domain table, and items overwhelmingly do not come from email.
- *Infer the link from the audit trail.* Rejected as fragile and unqueryable.

## D7: The inbox editing surface

**Decision**: A new `DraftEditModal.razor` following the `ShareTripModal` pattern — `[Parameter] EventCallback OnClose`, hosted from `InboxDrafts.razor`. `EditForm` + `DataAnnotationsValidator` + an `IValidatableObject` model, mirroring `TrackedItemForm`. A trip `InputSelect` populated from `TripApiClient.GetTripsAsync`, and a leg `InputSelect` repopulated from `GetDetailAsync` whenever the trip changes, with a "No trip leg yet" option.

**Rationale**: The user's request was explicit — the traveler must be able to assign a leg *and* clean up parsed fields before saving. `TrackedItemForm` cannot be reused directly: it is parameterized on `TripId`, edits a `TrackedItemDto`, and saves through the trip-items API. A draft has a different shape, a different save target, and a trip that is not yet chosen. Mirroring its structure gives consistency without forcing a shared component to serve two different lifecycles.

`GetTripsAsync` already returns owner and shared trips with an `AccessLevel`, so filtering to `Owner` and `Collaborator` satisfies FR-003 with no new endpoint.

**Alternatives considered**:

- *Generalize `TrackedItemForm` to serve both.* Rejected. It would need a nullable trip, a second save path, and a mode flag — more coupling than the duplication it saves.
- *Edit inline on the draft card.* Rejected. Eleven fields plus two pickers does not fit a list row, and the review queue would become unreadable.
- *Route the traveler to the trip page to place the draft.* Rejected — FR-009 and SC-004 explicitly require resolution without leaving the review queue.
