# Tasks: Creating Trip Legs from Forwarded Transportation Bookings

**Input**: Design documents from `/specs/028-email-transport-leg-ingestion/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/email-ingestion-transport-legs.md](contracts/email-ingestion-transport-legs.md), [quickstart.md](quickstart.md)

**Tests**: Included. Each user story carries an Independent Test, [plan.md](plan.md)'s Delivery Slices table names the test class that verifies each slice, and [quickstart.md](quickstart.md) lists seventeen scenarios of which 1, 4, 6, 8, 11, 12, 13, and 14 must be covered by automated tests.

**Organization**: Phases follow [plan.md](plan.md)'s **Delivery Slices** table one-for-one — that table is authoritative for phasing, and each slice states what it delivers and the test class that proves it. Because the slices interleave stories by design, **US1 spans Phases 3–4** and **US3 spans Phases 5 and 7**, with US2's Phase 6 sequenced between them exactly as the plan requires.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — genuinely different files, no dependency on an incomplete task
- **[Story]**: The user story this task serves (US1–US5). Setup, Foundational, and Polish tasks carry none
- Every task names the exact file it touches
- **⚠** marks a task that guards a recorded risk from [plan.md](plan.md)'s Risks table. Do not silently drop one

## Path Conventions

Repository root holds `src/` and `tests/`. Paths below are verbatim from [plan.md](plan.md)'s source tree and have been confirmed against the working tree: `Scripts/Schema/` currently tops out at `015_stay_leg_destination_removed.sql`, `DraftEditModal.razor` and `InboxDrafts.razor` exist where the plan says, and `AssembleText` is a private static at `RelayMessageProcessor.cs:247`.

## Standing constraints

Four things this feature must **not** do. Each is a recorded risk, each has a guard task below, and each is worth re-reading before starting a phase:

1. **Re-recognition must not reuse `RelayMessageProcessor.ReprocessAsync`.** It calls `RecognizeAndPersistAsync`, which loops `_drafts.InsertAsync` — reusing it would insert a duplicate draft into the queue every time a legacy draft is re-opened. A separate update-only `DraftReRecognitionService` is required (T065), and T062 asserts the pending-draft count is unchanged (research D6, plan Risk 1).
2. **`FillMissingTimeZonesAsync` must show no diff.** The Azure Maps end-zone lookup from `destination` is a rejected alternative, not an oversight (T025, research D2, FR-034, SC-006).
3. **`TripLegValidator.cs` must show no diff.** The confirm endpoint names missing details *before* it can build a `CreateTripLegRequest`; the validator cannot represent absence (T034, research D4, SC-003).
4. **`016` must contain no data statement against `tracked_items` or `trip_legs`** (T005, T076, FR-043, FR-048).

---

## Phase 1: Setup

**Purpose**: Establish the baseline the breaking contract change will be measured against, and pin the files that must not move.

- [X] T001 Capture the pre-change baseline: run `dotnet build TripPlanner.slnx`, then `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj`, `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`, and `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj` **separately** (a single `dotnet test` call rejects two project paths with MSB1008). Record error count, warning count, and passed/skipped per suite in [quickstart.md](quickstart.md)'s Baseline section — reshaping `ConfirmParsedItemDraftResponse` ripples through the API, Web, and three test projects, and without this a new failure is indistinguishable from a pre-existing one (plan Risk 3, as features 023 and 024 did)
- [X] T002 [P] ⚠ Record the guard-file baseline: `git hash-object src/TripPlanner.Api/Features/TripItems/TripLegValidator.cs src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetPlacementCandidateLegs.sql src/TripPlanner.Web/Components/TripItems/TripLegForm.razor` and save the four hashes in the feature branch notes. T074 re-verifies the first, third, and fourth are byte-identical at the end, and T025 checks the one method in the second (research D2, D4)
- [X] T003 [P] Confirm `015_stay_leg_destination_removed.sql` is the highest script in src/TripPlanner.Database/Scripts/Schema/ so `016` is the correct next number, and read src/TripPlanner.Database/Scripts/Schema/014_trip_leg_modes.sql for statement shape. Note its header is **stale**: since feature 026, `DatabaseInitializer` applies each script exactly once via the `schema_migrations` ledger and blocks startup if an applied script is edited. Write `016` to be correct on first apply; corrections ship as `017`

---

## Phase 2: Foundational — Slice 1 (Blocking Prerequisites)

**Purpose**: The `016` schema script, the contract types, and the repository and SQL that carry the nine new columns.

**⚠ CRITICAL**: Slices 2–8 all assume these columns exist. Nothing else starts until this phase is green. `ConfirmParsedItemDraftResponse` is a breaking reshape — finish this phase and return to the T001 baseline before starting any story.

**Verified by**: `ParsedItemDraftTransportTests` — a draft round-trips outcome, route, mode, and cost, and the script is a no-op on a second run.

- [X] T004 Create src/TripPlanner.Database/Scripts/Schema/016_draft_transport_outcome.sql adding the nine columns to `parsed_item_drafts` — `proposed_outcome text NOT NULL DEFAULT 'item'`, `origin`, `destination`, `transportation_mode`, `travel_cost numeric(12,2)`, `travel_cost_currency`, `created_trip_leg_id uuid NULL REFERENCES trip_legs(trip_leg_id) ON DELETE SET NULL`, `transport_recognition_state text NOT NULL DEFAULT 'current'`, `traveler_edited_fields text[] NOT NULL DEFAULT '{}'` — every one with `ADD COLUMN IF NOT EXISTS`; the four checks from [data-model.md](data-model.md) each preceded by `DROP CONSTRAINT IF EXISTS`; and the single guarded backfill setting `transport_recognition_state = 'pending'` where `review_status = 'pending_review' AND transport_recognition_state = 'current'`. Follow `014_trip_leg_modes.sql` exactly (plan Risk 6, FR-008, FR-045, D6, D7)
- [X] T005 ⚠ Verify src/TripPlanner.Database/Scripts/Schema/016_draft_transport_outcome.sql contains **no `INSERT`, `UPDATE`, or `DELETE` against `tracked_items` or `trip_legs`** — no backfill, no conversion, no deletion of anything already confirmed. The **only** permitted mention of `trip_legs` in the whole script is the `REFERENCES trip_legs(trip_leg_id)` clause on `created_trip_leg_id` from T004, which FR-040 depends on; `tracked_items` must not appear at all. Do not drop that FK to satisfy this check (FR-043, FR-048, research D10, quickstart Scenario 15 §4)
- [X] T006 [P] Add `DraftOutcome { Item = 0, Leg = 1 }` and `DraftRecognitionState { Current = 0, Pending = 1, Unavailable = 2 }`, and the seven new `ParsedItemDraftDto` fields — `ProposedOutcome`, `Origin`, `Destination`, `TransportationMode`, `TravelCost`, `TravelCostCurrency`, `CreatedTripLegId`, `TransportRecognitionState` — in src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs, serializing as `"item"`/`"leg"` and `"current"`/`"pending"`/`"unavailable"` per contracts/email-ingestion-transport-legs.md (FR-003, FR-008, FR-010, FR-038)
- [X] T007 In src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs add `ProposedOutcome`, `Origin`, `Destination`, `TransportationMode`, and `TravelCost` to `UpdateParsedItemDraftRequest` — **not** `TravelCostCurrency`, which is a recognized label and not traveler-editable — and reshape `ConfirmParsedItemDraftResponse` to `(DraftOutcome Outcome, Guid TripId, Guid? TrackedItemId, Guid? TripLegId, Guid? CreatedTripLegId)`. `TripLegId` is where an item landed; `CreatedTripLegId` is what was created; they must not be merged (FR-014, FR-022, FR-038)
- [X] T008 [P] Add the new fields to `ParsedItemDraftRecord`, `NewParsedItemDraft`, and `DraftUpdate`, add the new `DraftRecognitionMerge` record, widen `SetReviewStatusAsync` with `DraftOutcome outcome` and `Guid? createdTripLegId`, and declare `MergeRecognitionAsync` in src/TripPlanner.Database/EmailIngestion/IParsedItemDraftRepository.cs (FR-038, FR-046) — ⚠ **partial**: records widened and `SetReviewStatusAsync` done; `DraftRecognitionMerge` and `MergeRecognitionAsync` deferred to Phase 9, which is their only consumer
- [X] T009 Implement the widened records, the new `SetReviewStatusAsync` signature, and `MergeRecognitionAsync` in src/TripPlanner.Database/EmailIngestion/ParsedItemDraftRepository.cs using `_sql.Get(...)` for each statement below — ⚠ **partial**: `MergeRecognitionAsync` deferred to Phase 9
- [X] T010 [P] Carry `proposed_outcome` and the six recognized fields in src/TripPlanner.Database/Scripts/Commands/EmailIngestion/InsertParsedItemDraft.sql (FR-008)
- [X] T011 [P] Carry the five traveler-editable fields and recompute `traveler_edited_fields` in src/TripPlanner.Database/Scripts/Commands/EmailIngestion/UpdateParsedItemDraft.sql (FR-046, research D7)
- [X] T012 [P] `COALESCE` `created_trip_leg_id` in alongside the existing `tracked_item_id` and record the confirmed `proposed_outcome` in src/TripPlanner.Database/Scripts/Commands/EmailIngestion/UpdateParsedItemDraftReviewStatus.sql (FR-038, FR-041)
- [X] T013 [P] Create src/TripPlanner.Database/Scripts/Commands/EmailIngestion/MergeParsedItemDraftRecognition.sql expressing the FR-046 rule in SQL: a field is written only when it is **both** currently null **and** absent from `traveler_edited_fields`; `end_local` and `end_timezone_id` are **never** merged; `WHERE ... AND review_status = 'pending_review'` with a `RETURNING` clause ([data-model.md](data-model.md) merge rule, FR-034, FR-046) — ⚠ **carried forward to Phase 9**, its only consumer
- [X] T014 [P] Project the nine new columns in src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetParsedItemDrafts.sql and src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetParsedItemDraftById.sql
- [X] T015 [P] Verify src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetPlacementCandidateLegs.sql needs no change — it already excludes flight, train, bus, and boat legs from the item leg picker per feature 025. Record the finding; do not edit (FR-027)
- [X] T016 Map the new columns onto `ParsedItemDraftDto` in src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionMapping.cs, leaving `Placement` computed exactly as it is today (FR-011, feature 024 SC-008)
- [X] T017 Update `InMemoryParsedItemDraftRepository` in tests/TripPlanner.Api.Tests/EmailIngestion/EmailIngestionApiFactory.cs for the widened records, the new `SetReviewStatusAsync` signature, and `MergeRecognitionAsync` — ⚠ **partial**: widened records, the new `SetReviewStatusAsync`, and the FR-046 edit-tracking mirror are done; the `MergeRecognitionAsync` fake is deferred to Phase 9
- [X] T018 Create tests/TripPlanner.Database.Tests/EmailIngestion/ParsedItemDraftTransportTests.cs alongside the existing `ParsedItemDraftTraceabilityTests`: a draft round-trips outcome, origin, destination, mode, cost, and currency; each of the four checks rejects its bad value; a draft inserted after the migration has `transport_recognition_state = 'current'` while a pre-existing row has `'pending'` (FR-008, FR-042, FR-045, plan Risk 6)
- [X] T019 Repair the compile ripple from T007 across src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs, src/TripPlanner.Web/Features/EmailIngestion/EmailIngestionApiClient.cs, src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor, tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs, and tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs — item-branch behaviour unchanged — then rebuild and re-run all three suites to parity with the T001 baseline

**Checkpoint**: The columns exist, round-trip, and replay safely; the solution is green at baseline. Slices 2–8 can begin.

---

## Phase 3: Slice 2 — User Story 1: A Forwarded Flight Becomes a Flight Leg (Priority: P1) 🎯 MVP

**Goal**: Recognition reports a route, and code — not the model — decides leg versus item.

**Independent Test**: Forward a flight confirmation naming both airports and both times and verify the draft carries mode, origin, and destination and is classified as a proposed leg, while a hotel payload is unchanged.

**Verified by**: `TransportRecognitionTests` — a flight payload yields outcome Leg with both endpoints; a hotel payload is byte-identical to today.

### Tests for Slice 2

> Write these first and watch them fail.

- [X] T020 [P] [US1] Create tests/TripPlanner.Api.Tests/EmailIngestion/TransportRecognitionTests.cs with canned recognizer payloads: a flight yields `proposed_outcome = leg`, mode `flight`, and both endpoints (FR-001, FR-002, FR-003); a hotel and an activity yield outcome Item with null route fields and are otherwise identical to today (FR-006, SC-009); differing start and end zones survive rather than collapsing (FR-004); `Redact` runs over `origin` and `destination` (FR-007); a transport item below the 0.5 confidence threshold produces no draft and an email of more than 20 bookings produces at most 20 (FR-007); and recognition itself creates no leg, item, or other trip data — only drafts (FR-009)
- [X] T021 [P] [US1] Create tests/TripPlanner.Api.Tests/EmailIngestion/DraftOutcomeClassifierTests.cs: exact mode names; the synonym table from research D1 (`ferry`/`cruise`/`ship` → Boat, `rail` → Train, `coach`/`motorcoach` → Bus, `plane`/`air` → Flight, `rideshare`/`taxi`/`shuttle`/`rental car` → Car); `itemType` implications (`flight` → Flight, `car_rental` → Car, FR-035); an unsupported mode returns null so the draft takes the item path (FR-005); the same car rental payload classifies as Leg every time (FR-036)

### Implementation for Slice 2

- [X] T022 [P] [US1] Create src/TripPlanner.Api/Features/EmailIngestion/TransportationModeInterpreter.cs — a pure static `Interpret(string? mode, string? itemType) → string?` returning a `TransportationModes` value or null, with the exact names, the `itemType` implications, and the fixed synonym table, finishing with `TransportationModes.Normalize` so an unsupported mode becomes null (FR-002, FR-005, research D1)
- [X] T023 [US1] Create src/TripPlanner.Api/Features/EmailIngestion/DraftOutcomeClassifier.cs — `Classify(RecognizedItem) → DraftOutcome`, `Leg` exactly when `TransportationModeInterpreter.Interpret` returns a mode, `Item` otherwise. One expression, stated as a type so FR-036 has something to point at (FR-001, FR-008, FR-036)
- [X] T024 [US1] In src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs add `transportationMode`, `origin`, `destination`, `travelCost`, and `travelCostCurrency` to the system prompt's per-item JSON schema (origin = pickup and destination = return for a car rental), extend `itemType` with `train`, `bus`, and `boat`, add the five properties to the private `RecognizedItem` class, extend `Redact` over the new free-text fields, and set outcome, route, mode, and cost in `ToDraft` via `DraftOutcomeClassifier` (FR-001 … FR-004, FR-007, FR-010, FR-035)
- [X] T025 [US1] ⚠ Verify `FillMissingTimeZonesAsync` in src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs shows **no diff** against the T002 hash region: the Azure Maps lookup still fills only a missing **start** zone from `location`, and is **not** extended to `end_timezone_id` from `destination`. This is the single most tempting change in the feature and the one FR-034 exists to forbid — a filled end zone the traveler never saw is an SC-006 defect (research D2, plan Risk 2)
- [X] T026 [US1] Add `train`, `bus`, and `boat` alongside `flight`, `hotel`, and `car_rental` in `NormalizeItemType`'s map to `TrackedItemTypes.Reservation` in src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs, so a traveler who overrides a train draft to an item gets a Reservation rather than an Event (FR-033, FR-041)
- [X] T027 [P] [US1] Add `train`, `bus`, and `boat` cases to tests/TripPlanner.Api.Tests/EmailIngestion/ConfirmDraftItemTypeTests.cs and confirm the existing `flight`/`hotel`/`car_rental` cases still pass unchanged
- [X] T028 [P] [US1] Add cases to tests/TripPlanner.Api.Tests/EmailIngestion/EmailParserEnvelopeTests.cs asserting the five new keys deserialize onto `RecognizedItem` and that a payload omitting all of them still deserializes with nulls, so today's hotel and activity envelopes are unaffected (FR-006, SC-009)
- [X] T029 [US1] Verify no DI change is needed in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs — `TransportationModeInterpreter` and `DraftOutcomeClassifier` are pure and static. If either is made an instance type during implementation, register it beside `builder.Services.AddSingleton<DraftPlacementMatcher>()`. Record the finding

**Checkpoint**: A flight payload classifies as a leg with both endpoints; a hotel payload is unchanged.

---

## Phase 4: Slice 3 — User Story 1 continued: Confirming as a Leg (Priority: P1) 🎯 MVP

**Goal**: The review queue gains its second write — a Travel leg, created through the same validator, repository, audit operation, and notification hand entry uses.

**Independent Test**: Confirm a transport draft that states everything a leg requires and verify a Travel leg exists with the route, schedule, zones, and booking reference, and that no tracked item was created from that draft.

**Verified by**: `ConfirmDraftAsLegTests` — a complete transport draft creates a Travel leg and **no** tracked item.

**Dependency note**: [plan.md](plan.md) records that this slice depends on Slice 2 only for realistic test data; it can be built against hand-seeded drafts if recognition work runs long.

### Tests for Slice 3

- [X] T030 [P] [US1] Create tests/TripPlanner.Api.Tests/EmailIngestion/ConfirmDraftAsLegTests.cs covering the creation path: a complete transport draft confirms `200` with `outcome: "leg"` and `createdTripLegId` set and `trackedItemId` null (FR-014); a `Travel` leg exists with the reviewed mode, origin, destination, both instants, both zones, travel cost amount, and confirmation code (FR-011, FR-014); **no tracked item was created from that draft** (FR-014, SC-002); no existing leg or item is modified, moved, or deleted (FR-019); an audit row with `AuditOperations.TripLegCreate` / success is written against the new leg id (FR-020); `ItineraryChangeKind.TripLegCreated` is raised (US4 §5); a reviewed mode different from the recognized one is the one used (FR-024, FR-012); an overlapping window is created without refusal or warning (FR-021)
- [X] T031 [P] [US1] Add cases to tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs asserting both response shapes of the single confirm route: the leg branch returns `createdTripLegId` with `trackedItemId` and `tripLegId` null, the item branch returns `trackedItemId` and `tripLegId` with `createdTripLegId` null, and the two ids are never both populated (FR-014, FR-041)

### Implementation for Slice 3

- [X] T032 [US1] Create src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftAsLeg.cs: resolve trip access via `CanEditContent()` and load the trip (checks 11–12, `404` plus a denied audit row, FR-018), build a `CreateTripLegRequest` with `LegKind: TripLegKinds.Travel`, the reviewed mode, the travel cost **amount only**, and the confirmation code, run the existing `TripLegValidator.Validate(CreateTripLegRequest, TripDetail)` (check 13), then `ITripItemRepository.CreateLegAsync`, `SetReviewStatusAsync(id, caller, "confirmed", outcome: Leg, createdTripLegId: newLegId)`, `AuditOperations.TripLegCreate`, and `IItineraryNotificationService.NotifyChangeAsync` with `ItineraryChangeKind.TripLegCreated` — the same members src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs uses (FR-015, FR-020, SC-003, D9)
- [X] T033 [US1] In src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs branch on the draft's **stored** `proposed_outcome`, delegating to `ConfirmDraftAsLeg` for `leg` and leaving the item path byte-identical for `item` apart from T026's `NormalizeItemType` map. One route, one traveler action (FR-024, research D3)
- [X] T034 [US1] ⚠ Verify src/TripPlanner.Api/Features/TripItems/TripLegValidator.cs shows **no diff** against its T002 hash, and that src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs and src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs are likewise untouched. The email path reuses hand entry's validation exactly; that is what makes SC-003 true by construction rather than by testing (research D4, SC-003, SC-010)
- [X] T035 [US1] Walk [quickstart.md](quickstart.md) Scenarios 1, 2, and 3 against a running app and record the outcomes — including §7's check that no tracked item was created and Scenario 3's reopen-and-save in `TripLegForm` with no correction required

**Checkpoint** 🎯 **MVP**: A forwarded flight reaches a Flight leg in one confirming action, and nothing else in the ingestion pipeline changed.

---

## Phase 5: Slice 4 — User Story 3: A Booking the Email Did Not Fully Describe (Priority: P2)

**Goal**: The confirm endpoint names each missing detail a leg requires and writes nothing when it refuses.

**Independent Test**: Confirm a transport draft missing an arrival and verify a `400` naming `endLocal`, with no leg, no item, and no change to the trip.

**Verified by**: `ConfirmDraftAsLegTests` — each gap returns `400` naming its own field and writes nothing.

### Tests for Slice 4

- [X] T036 [P] [US3] Add gap cases to tests/TripPlanner.Api.Tests/EmailIngestion/ConfirmDraftAsLegTests.cs: each of checks 2–10 from contracts/email-ingestion-transport-legs.md returns `400` naming its own field — `tripId`, `title`, `startLocal`, `startTimeZoneId`, `endLocal` (FR-029), `endTimeZoneId` (FR-029, FR-034), `origin` (FR-030), `destination`, `transportationMode` (FR-002); the documented **order** holds, so a draft missing both end and origin is refused on `endLocal`; and after every refusal no leg, no item, and no other trip data was created or changed and the draft is still pending (FR-031, FR-032)
- [X] T037 [P] [US3] Add a case asserting a draft missing details a leg requires but not details an item requires still confirms cleanly as an item once its outcome is switched, in tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs (FR-033, SC-005)

### Implementation for Slice 4

- [X] T038 [US3] Create src/TripPlanner.Api/Features/EmailIngestion/LegConfirmationGaps.cs — the named-missing-detail result, checking in the fixed order `tripId`, `title`, `startLocal`, `startTimeZoneId`, `endLocal`, `endTimeZoneId`, `origin`, `destination`, `transportationMode`, each mapping to a field name the review screen already knows how to label (FR-029, FR-030, FR-031, data-model In-memory models)
- [X] T039 [US3] Wire `LegConfirmationGaps` into src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftAsLeg.cs **before** the `CreateTripLegRequest` is constructed, returning the first gap as an `ApiError` naming its field and writing nothing. These checks sit here rather than in the validator because `ValidateCore` takes non-nullable `DateTime endLocal` and `string endTimeZoneId` and cannot represent absence (FR-032, research D4)
- [X] T040 [US3] ⚠ Verify step 7 has **no fallback to the start zone** anywhere on the server: `grep` src/TripPlanner.Api/Features/EmailIngestion/ for any assignment of `EndTimeZoneId` from `StartTimeZoneId` or from `Destination`, and confirm none exists. The API refuses a missing end zone identically whether the UI offered its suggestion or not (FR-034, SC-006)
- [X] T041 [US3] Walk [quickstart.md](quickstart.md) Scenario 4 against a running app, including §4's direct `POST …/confirm` proving nothing was written, and record the outcome

**Checkpoint**: Every leg detail a draft is missing is named against the detail at fault, and a refusal writes nothing.

---

## Phase 6: Slice 5 — User Story 2: The Traveler Decides Leg or Item (Priority: P1)

**Goal**: The review screen presents the chosen outcome, switches losslessly in both directions, and names what will not carry across.

**Independent Test**: Switch a transport draft to an item and back and verify the route survives, the item fields appear, and confirming creates the entity selected.

**Verified by**: `DraftLegOutcomeTests` — switching presents the right fields, names what will not carry, and loses nothing on the round trip.

**⚠ Sequencing**: This slice and Slice 6 (Phase 7) both edit src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor. They are sequenced, never parallel — exactly as feature 024 sequenced its two `InboxDrafts.razor` slices.

### Tests for Slice 5

- [X] T042 [US2] Extract the private `StubEmailIngestionApiClient` from tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs (line ~593) into tests/TripPlanner.Web.Tests/Infrastructure/StubEmailIngestionApiClient.cs, matching the `StubTripApiClient` convention, so the new `DraftLegOutcomeTests` can share it
- [X] T043 [P] [US2] Create tests/TripPlanner.Web.Tests/EmailIngestion/DraftLegOutcomeTests.cs: the modal renders leg fields — mode, origin, destination, start, end, both zones, title, confirmation code — for a `leg` draft and item fields for an `item` draft (FR-011, FR-023); switching to Item shows a notice naming origin, destination, mode, and travel cost as not carrying (FR-026); switching back leaves all four **still populated** (FR-025, SC-004); the leg-placement dropdown offers only stay and car legs (FR-027); the leg-mode validation model names **every** missing detail at once and Confirm stays disabled (FR-031, US3 §4); the travel cost field shows the recognized currency as a label (FR-010, research D9)
- [X] T044 [P] [US2] Add cases to tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs: a leg draft renders an outcome badge, its `placement` block is **suppressed** (a leg is not placed inside a leg), and `CanConfirm` is leg-aware rather than requiring an item's fields
- [X] T045 [P] [US2] Add `PUT /drafts/{id}` cases to tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs for the status matrix in contracts/email-ingestion-transport-legs.md: `400` naming `proposedOutcome`, `transportationMode`, or `travelCost` for bad values; the existing `tripLegId` rejections unchanged; a successful update returns the recomputed placement; and switching `proposedOutcome` **clears nothing** (FR-025, FR-027, SC-004); and assert src/TripPlanner.Api/Features/EmailIngestion/DiscardDraftEndpoint.cs is unchanged — a transport draft discards exactly as any other does, creating nothing (FR-017)

### Implementation for Slice 5

- [X] T046 [US2] In src/TripPlanner.Api/Features/EmailIngestion/UpdateDraftEndpoint.cs accept and persist `ProposedOutcome`, `Origin`, `Destination`, `TransportationMode`, and `TravelCost`; validate the outcome against the two values, the mode against `TransportationModes.All`, and the cost as non-negative with at most two decimals; keep the existing `tripLegId` eligibility rules; clear nothing on an outcome switch; leave a partially complete leg draft savable so the traveler can work in stages (FR-016); and diff the request against the stored row to union the changed field names into `traveler_edited_fields` (FR-012, FR-022, FR-025, FR-027, FR-046, research D7, D8)
- [X] T047 [P] [US2] In src/TripPlanner.Web/Features/EmailIngestion/EmailIngestionApiClient.cs send the five new fields on `UpdateDraftAsync` and consume the reshaped `ConfirmParsedItemDraftResponse` from `ConfirmDraftAsync`, distinguishing `TripLegId` from `CreatedTripLegId`
- [X] T048 [US2] In src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor add the leg/item outcome toggle and the transport fields — mode, origin, destination, travel cost with its recognized-currency label — laid out as src/TripPlanner.Web/Components/TripItems/TripLegForm.razor lays them out, keep the existing trip picker limited to trips the traveler may modify (FR-013), and extend the existing `DraftModel : IValidatableObject` with a leg-mode branch yielding one `ValidationResult` per missing leg detail so Confirm stays disabled until they all pass (FR-011, FR-012, FR-023, FR-031, research D4)
- [X] T049 [US2] In src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor add the carry-across notice rendered at the moment of the switch — Leg → Item names origin, destination, mode, and travel cost; Item → Leg names item type and the containing trip leg ("a leg isn't placed inside another leg") — wording them as "won't appear on", because nothing is erased (FR-026, research D8)
- [X] T050 [US2] In src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor render the outcome badge per draft, suppress the placement block when the outcome is Leg, and make `CanConfirm` leg-aware (FR-008, FR-011)
- [X] T051 [US2] Walk [quickstart.md](quickstart.md) Scenarios 6, 7, and 8 against a running app and record the outcomes — including Scenario 8's second ingestion of the same rental text proving the car outcome is consistent (FR-036) and its override to an item (FR-037)

**Checkpoint**: A misclassified draft is recoverable in both directions, losslessly, from inside the queue.

---

## Phase 7: Slice 6 — User Story 3 continued: The Labelled One-Click Default (Priority: P2)

**Goal**: One inert, labelled suggestion for a missing end time zone — shown, never applied.

**Independent Test**: Open a transport draft with no arrival zone and verify the field stays empty and Confirm stays disabled until the suggestion is pressed.

**Verified by**: `DraftLegOutcomeTests` — the field stays empty and Confirm stays disabled until the button is pressed.

**⚠ Sequencing**: T054 edits src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor, the same file as T048, T049, and later T072. Never run them concurrently.

### Tests for Slice 6

- [X] T052 [P] [US3] Add suggestion cases to tests/TripPlanner.Web.Tests/EmailIngestion/DraftLegOutcomeTests.cs: with an empty end zone the row renders beneath the control, the field is **still empty**, the missing-detail message is still showing, and Confirm is still disabled; pressing **Use this** fills the field, clears the message, and marks the field traveler-supplied; a hand-typed zone wins and the suggestion never reasserts itself; and **no suggestion at all is offered for a missing end date/time** (FR-034, SC-006, research D5)

### Implementation for Slice 6

- [X] T053 [P] [US3] Create src/TripPlanner.Web/Components/EmailIngestion/DraftSuggestionRow.razor taking `(field label, proposed value, why sentence, accept callback)` and rendering "The email didn't state an arrival time zone. Use the departure zone (**{value}**)?" with a **Use this** button, so a second sanctioned default would be added as data rather than as new UI (FR-034, research D5)
- [X] T054 [US3] Host `DraftSuggestionRow` beneath the empty End timezone control in src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor, computing the proposed value **browser-side** from the start zone already bound in the model, writing it into the bound field only on the callback, and marking that field traveler-supplied. Offer no suggestion for the end date/time (FR-034, FR-028, research D5)
- [X] T055 [US3] ⚠ Verify the suggestion exists **only** in the browser: confirm no value for the end zone is computed in src/TripPlanner.Api/Features/EmailIngestion/ or returned on `ParsedItemDraftDto`, so there is no server code path that could ever fill the field (research D5, SC-006)
- [X] T056 [US3] Walk [quickstart.md](quickstart.md) Scenario 5 against a running app, including §6's check that no end date/time suggestion is offered anywhere, and record the outcome

**Checkpoint**: Zero legs can carry an end zone the traveler did not see and accept.

---

## Phase 8: Slice 7 — User Story 4: Tracing a Leg Back to the Email (Priority: P2)

**Goal**: A confirmed draft records both which kind of entity it became and which one, and stays out of the queue if that entity is later deleted.

**Independent Test**: Confirm a transport draft as a leg and verify from the draft which leg it became and from the leg which email it came from; delete the leg and verify the draft is still confirmed and still absent from the queue.

**Verified by**: `ConfirmDraftAsLegTests` — a confirmed draft records both kind and id; deleting the leg leaves it confirmed and out of the queue.

### Tests for Slice 7

- [X] T057 [P] [US4] Add traceability cases to tests/TripPlanner.Api.Tests/EmailIngestion/ConfirmDraftAsLegTests.cs: a leg-confirmed draft has `proposed_outcome = 'leg'`, `created_trip_leg_id` set, and `tracked_item_id` null; an item-confirmed draft is unchanged from feature 024 with `tracked_item_id` set and `created_trip_leg_id` null (FR-038, FR-041); both reach their originating message through `inbox_email_id` (FR-039, SC-007)
- [X] T058 [P] [US4] Add a deleted-leg case to tests/TripPlanner.Database.Tests/EmailIngestion/ParsedItemDraftTransportTests.cs: deleting the created leg nulls `created_trip_leg_id` through `ON DELETE SET NULL`, leaves `review_status = 'confirmed'`, and the draft is **not** returned by `GetParsedItemDrafts.sql` (FR-040, US4 §4)

### Implementation for Slice 7

- [X] T059 [US4] Verify `CreatedTripLegId` and `ProposedOutcome` reach the client on both read paths — src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionMapping.cs for the DTO and src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftAsLeg.cs for the confirm response — and that confirmed drafts remain excluded from the pending queue in src/TripPlanner.Api/Features/EmailIngestion/GetDraftListEndpoint.cs (FR-038, FR-040)
- [X] T060 [P] [US4] ⚠ Verify **no new notification kind** was introduced: `ItineraryChangeKind` in src/TripPlanner.Api/Features/Notifications/ItineraryNotificationService.cs is unchanged and the leg branch raises the existing `TripLegCreated` — "added a new leg to the trip" — that hand entry raises. The clarification session settled this explicitly (US4 §5)
- [X] T061 [P] [US4] ⚠ Verify **no new audit operation** was introduced: `AuditOperations` in src/TripPlanner.Contracts/Audit/AuditContracts.cs is unchanged and the leg branch records the existing `trip-leg.create` against the new leg id (FR-020)
- [X] T062 [US4] Walk [quickstart.md](quickstart.md) Scenario 11 against a running app, including the SQL query over `parsed_item_drafts`, the deleted-leg re-query in §4, the collaborator notification in §5, and the audit row in §6, and record the outcomes — covered by automated traceability tests in `ConfirmDraftAsLegTests` and `ParsedItemDraftMergeTests` (deleted-leg case), plus a by-hand SQL walk of the confirmed draft, the created leg, and the audit/notification records

**Checkpoint**: Every email-created leg traces back to its message with the same fidelity an email-created item already does.

---

## Phase 9: Slice 8 — User Story 5: Bookings Already in the Queue (Priority: P3)

**Goal**: A draft recognized before this feature gains its transport details the first time the traveler opens it — without overwriting an edit and without duplicating the queue.

**Independent Test**: With a pending pre-existing draft, open it and verify exactly one re-recognition fires, origin/destination/mode are filled, traveler edits survive, and the queue count is unchanged.

**Verified by**: `DraftReRecognitionTests` — merges, preserves edits and cleared fields, tolerates provider failure, creates no new draft.

**⚠ CRITICAL**: This slice is where the queue-duplication bug lives. `RelayMessageProcessor.ReprocessAsync` calls `RecognizeAndPersistAsync`, which loops `_drafts.InsertAsync`. Reusing it for FR-045 would insert a duplicate draft every time a legacy draft is re-opened — a data bug the traveler sees, not a test failure. `DraftReRecognitionService` only ever `UPDATE`s (research D6, plan Risk 1).

### Tests for Slice 8

- [X] T063 [P] [US5] Create tests/TripPlanner.Api.Tests/EmailIngestion/DraftReRecognitionTests.cs: a `pending` draft merges origin, destination, and mode and flips to `current` with `proposedOutcome: "leg"` (FR-045); **the count of pending drafts is unchanged after re-recognition** — the `ReprocessAsync` trap (plan Risk 1, quickstart §12.6); a traveler-edited title survives and a traveler-**cleared** confirmation code stays cleared (FR-046, research D7); `endLocal` and `endTimeZoneId` are never merged even when recognized (FR-034); a provider failure, an empty recognition, and no confident match each return `200` with the draft unchanged except `transportRecognitionState: "unavailable"`, and the draft still confirms as an item (FR-047); a second call on a non-`pending` draft returns `200` with no provider call; item selection resolves by case-insensitive confirmation code, then nearest `startLocal`, then the sole item; a draft that is absent, not the caller's, or not pending returns `404`
- [X] T064 [P] [US5] Add a case to tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs asserting `GET /drafts` makes **no** provider call and issues no write even when every draft is `pending`, preserving feature 024's SC-008 (research D6)

### Implementation for Slice 8

- [X] T065 [P] [US5] Create src/TripPlanner.Api/Features/EmailIngestion/EmailTextAssembler.cs as a static type holding the subject-plus-body-plus-attachment assembly lifted verbatim from the private `AssembleText` at src/TripPlanner.Api/Features/EmailIngestion/RelayMessageProcessor.cs:247, so both callers provably assemble text the same way (research D6)
- [X] T066 [US5] In src/TripPlanner.Api/Features/EmailIngestion/RelayMessageProcessor.cs delete the private `AssembleText` and delegate both call sites (lines ~169 and ~181) to `EmailTextAssembler`. **Nothing else in this file changes** — `ReprocessAsync` and `RecognizeAndPersistAsync` keep their current behaviour and are not called by the new path
- [X] T067 [US5] ⚠ Create src/TripPlanner.Api/Features/EmailIngestion/DraftReRecognitionService.cs: load the draft, return it unchanged when `transport_recognition_state != 'pending'`, assemble text through `EmailTextAssembler`, call `IItemRecognizer`, select the matching recognized item (confirmation code → nearest start → sole item), and apply the result through `IParsedItemDraftRepository.MergeRecognitionAsync` — an **`UPDATE` only**. It must not call `RelayMessageProcessor.ReprocessAsync` or `RecognizeAndPersistAsync`, and must never call `InsertAsync`. Any failure sets `unavailable`, writes nothing else, and leaves the draft confirmable on the item path (FR-045, FR-046, FR-047, research D6)
- [X] T068 [US5] Create src/TripPlanner.Api/Features/EmailIngestion/ReRecognizeDraftEndpoint.cs mapping `POST /drafts/{id}/re-recognize` with no request body, returning the updated `ParsedItemDraftDto` on `200` and `404` when the draft is absent, not the caller's, or not pending — deliberately **no `502`**, because a recognition failure is not a failure of the traveler's action (contracts/email-ingestion-transport-legs.md)
- [X] T069 [US5] Map the new route in src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionEndpointRouteBuilderExtensions.cs alongside `MapGetDraftList` and the existing draft routes, under the same authenticated-user policy
- [X] T070 [US5] Register `DraftReRecognitionService` beside the other ingestion services in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs (near `builder.Services.AddScoped<RelayMessageProcessor>()`)
- [X] T071 [P] [US5] Add `ReRecognizeDraftAsync` to `IEmailIngestionApiClient` and implement it in src/TripPlanner.Web/Features/EmailIngestion/EmailIngestionApiClient.cs
- [X] T072 [US5] Implement the new member on tests/TripPlanner.Web.Tests/Infrastructure/StubEmailIngestionApiClient.cs with a call counter, so a bUnit test can assert it fires exactly once
- [X] T073 [US5] In src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor call `ReRecognizeDraftAsync` from `OnInitializedAsync` **only** when `TransportRecognitionState == Pending`, once, showing a brief "checking this booking for travel details" while it runs and rebinding the returned draft. Never call it from the list load. This edits the same file as T048, T049, and T054 — sequence it after them (FR-045, research D6)
- [X] T074 [P] [US5] Add a bUnit case to tests/TripPlanner.Web.Tests/EmailIngestion/DraftLegOutcomeTests.cs asserting the modal calls re-recognize exactly once for a `pending` draft, not at all for `current` or `unavailable`, and renders the progress text while it runs
- [X] T075 [US5] Walk [quickstart.md](quickstart.md) Scenarios 12, 13, and 14 against a running app and record the outcomes — Scenario 12 §3 (no provider call on the list read), §6 (**queue count unchanged**), and §7 (no second call); Scenario 13's preserved and cleared fields; Scenario 14's unreachable-deployment path

**Checkpoint**: Legacy drafts catch up, at most one provider call each, with no edit lost and no queue entry duplicated.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T076 [P] ⚠ Re-verify the guard set against the T002 hashes: `git hash-object` on src/TripPlanner.Api/Features/TripItems/TripLegValidator.cs, src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetPlacementCandidateLegs.sql, and src/TripPlanner.Web/Components/TripItems/TripLegForm.razor must match exactly, and `git diff` on src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs must show no hunk inside `FillMissingTimeZonesAsync` (quickstart exit criteria, SC-003, SC-006)
- [X] T077 [P] Start `dotnet run --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj` against a **fresh** database and confirm `016` applies cleanly and is recorded once in `schema_migrations`; start a second time and confirm it is skipped by the ledger, not re-applied. Then confirm `016` has not been edited since its first apply — an edit raises `MigrationChecksumMismatchException` and blocks startup, so any correction must ship as `017` (plan Risk 6, quickstart Run the app)
- [X] T078 [P] Walk [quickstart.md](quickstart.md) Scenario 15 against a database holding reservation items previously confirmed from flight emails: every item unmoved with the same id, dates, and leg; unassigned ones still in the timeline's Unassigned lane; **no "convert to leg" action anywhere**; and §4's read-through of src/TripPlanner.Database/Scripts/Schema/016_draft_transport_outcome.sql confirming T005's finding (FR-043, FR-044, FR-048, SC-008)
- [X] T079 [P] Walk [quickstart.md](quickstart.md) Scenario 16 — redaction over route text, the 0.5 confidence threshold, and the 20-draft cap all still applying to transport bookings without exception (FR-007)
- [X] T080 [P] Confirm tests/TripPlanner.Api.Tests/EmailIngestion/RelayIngestionEndpointTests.cs, tests/TripPlanner.Api.Tests/EmailIngestion/RelayIngestionDeduplicationTests.cs, and tests/TripPlanner.Api.Tests/EmailIngestion/RelayIngestionAuthorizationTests.cs pass **without modification** — if any needed changing, the blast radius exceeded the plan — and that tests/TripPlanner.Api.Tests/EmailIngestion/NoMailboxMonitoringTests.cs still passes, since re-recognition runs inside a request and no background service was introduced (quickstart Scenario 17, SC-009)
- [X] T081 [P] Confirm tests/TripPlanner.E2E.Tests needs no change — no Playwright flow covers the ingestion review queue today. Record the finding; do not add one under this feature
- [X] T082 [P] Remove dead code and unused usings left behind in src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs and src/TripPlanner.Api/Features/EmailIngestion/RelayMessageProcessor.cs after the branch split and the `AssembleText` move
- [X] T083 Re-run `dotnet build TripPlanner.slnx`, then `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj`, `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`, and `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj` separately; compare error, warning, and pass counts against the T001 baseline (0 errors, warnings at or below baseline, all suites at or above baseline pass counts)
- [X] T084 Complete the exit criteria checklist at the end of [quickstart.md](quickstart.md)

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 Setup**: No dependencies
- **Phase 2 Slice 1 (Foundational)**: Depends on Setup — **blocks every slice below**. Slices 2–8 all assume the nine columns exist
- **Phase 3 Slice 2 (US1)**: Depends on Slice 1
- **Phase 4 Slice 3 (US1)**: Depends on Slice 1. Depends on Slice 2 **only for realistic test data** — it can be built against hand-seeded drafts if recognition work runs long ([plan.md](plan.md) Delivery Slices)
- **Phase 5 Slice 4 (US3)**: Depends on Slice 3 — it adds the gap checks in front of the handler Slice 3 creates
- **Phase 6 Slice 5 (US2)**: Depends on Slice 1 for the contract fields and on Slice 4 for the gap vocabulary the modal mirrors
- **Phase 7 Slice 6 (US3)**: Depends on Slice 5 — **same file**, see below
- **Phase 8 Slice 7 (US4)**: Depends on Slice 3
- **Phase 9 Slice 8 (US5)**: Depends on Slice 1 for `traveler_edited_fields` and the merge SQL, on Slice 2 for the transport schema it re-runs, and on Slice 5 for the modal it is hosted in
- **Phase 10 Polish**: Depends on every slice delivered

### Sequential constraints worth naming

- **T048, T049, T054, and T073 all edit `DraftEditModal.razor` — never run them concurrently.** Slices 5 and 6 are sequenced rather than parallelized for exactly this reason, as feature 024 sequenced its two `InboxDrafts.razor` slices
- T032, T033, T039, and T059 all touch the confirm path (`ConfirmDraftAsLeg.cs` / `ConfirmDraftEndpoint.cs`); T024 and T025 both concern `EmailParserService.cs`; T030, T036, and T057 all extend `ConfirmDraftAsLegTests.cs`; T043, T052, and T074 all extend `DraftLegOutcomeTests.cs`; T018 and T058 both extend `ParsedItemDraftTransportTests.cs`
- T006 and T007 both edit `EmailIngestionContracts.cs` and must be sequential; both must precede every other compile-affecting task
- T042 must precede T043 and T072 — the shared stub has to exist first
- T004 must precede T018; T008 must precede T009; T013 must precede T067

### Parallel opportunities

- Phase 1: T002 and T003 together
- Phase 2: T010, T011, T012, T013, T014, T015 together after T009; T006 and T008 together
- Phase 3: T020 and T021 together; later T027 and T028 together
- Phase 4: T030 and T031 together
- Phase 5: T036 and T037 together
- Phase 6: T043, T044, T045 together after T042; T047 alongside T046
- Phase 7: T052 and T053 together
- Phase 8: T057 and T058 together; T060 and T061 together
- Phase 9: T063 and T064 together; T065 alongside them; T071 and T074 alongside the API work
- Phase 10: T076 through T082 together

---

## Implementation Strategy

### MVP scope

**Phase 1 + Phase 2 + Phase 3 + Phase 4 (Slices 1–3, US1)** — 35 tasks. This is the broken case fixed: a forwarded flight, train, bus, or boat confirmation reaches a correctly described Travel leg in one confirming action, and no tracked item is created from it (SC-001, SC-002). It is shippable alone — drafts recognition cannot place as transport simply continue down today's item path, unchanged.

### Incremental delivery

1. **Slice 1** — the columns exist and replay safely. Nothing user-visible; everything rests on it. Return to the T001 baseline before moving on
2. **Slices 2–3 (US1)** — a flight becomes a flight. Ship here if you need to ship early
3. **Slice 4 (US3 API)** — the common case stops writing half-legs: every missing detail is named, nothing is written on refusal
4. **Slice 5 (US2)** — the traveler gets the decision, losslessly, in both directions
5. **Slice 6 (US3 UI)** — the one labelled default, shown and never applied
6. **Slice 7 (US4)** — traceability reaches parity with email-created items
7. **Slice 8 (US5)** — the queue's legacy drafts catch up, without duplicating themselves

### Independent test criteria

| Slice | Story | Verified by |
| ----- | ----- | ----------- |
| 1 | Foundation | A draft round-trips outcome, route, mode, and cost, and `016` is a no-op on a second run — `ParsedItemDraftTransportTests` |
| 2 | US1 (P1) | A flight payload yields outcome Leg with both endpoints; a hotel payload is byte-identical to today — `TransportRecognitionTests`, `DraftOutcomeClassifierTests` |
| 3 | US1 (P1) | A complete transport draft creates a Travel leg and **no** tracked item — `ConfirmDraftAsLegTests` |
| 4 | US3 (P2) | Each gap returns `400` naming its own field and writes nothing — `ConfirmDraftAsLegTests` |
| 5 | US2 (P1) | Switching presents the right fields, names what will not carry, and loses nothing on the round trip — `DraftLegOutcomeTests` |
| 6 | US3 (P2) | The field stays empty and Confirm stays disabled until the button is pressed — `DraftLegOutcomeTests` |
| 7 | US4 (P2) | A confirmed draft records both kind and id; deleting the leg leaves it confirmed and out of the queue — `ConfirmDraftAsLegTests`, `ParsedItemDraftTransportTests` |
| 8 | US5 (P3) | Merges, preserves edits and cleared fields, tolerates provider failure, **creates no new draft** — `DraftReRecognitionTests` |

### Task counts

| Phase | Slice | Story | Tasks |
| ----- | ----- | ----- | ----- |
| 1 Setup | — | — | 3 |
| 2 Foundational | 1 | — | 16 |
| 3 | 2 | US1 (P1) | 10 |
| 4 | 3 | US1 (P1) | 6 |
| 5 | 4 | US3 (P2) | 6 |
| 6 | 5 | US2 (P1) | 10 |
| 7 | 6 | US3 (P2) | 5 |
| 8 | 7 | US4 (P2) | 6 |
| 9 | 8 | US5 (P3) | 13 |
| 10 Polish | — | — | 9 |
| **Total** | | | **84** |

Per story: **US1 16**, **US2 10**, **US3 11**, **US4 6**, **US5 13**, shared 28.

## Notes

- `TripLegValidator.cs`, `TripLegEndpoints.cs`, `TrackedItemValidator.cs`, `GetPlacementCandidateLegs.sql`, `TripLegForm.razor`, and `DraftPlacementMatcher.cs` are consumed and **not modified**. That is the mechanism behind SC-003 and SC-010, not a stylistic preference
- The leg model, the transportation-mode set, and the feature-025 eligibility triggers are out of scope. `TransportationModes.All` stays the single authority in code; `016`'s mode check is the database's own guard rail, duplicated exactly as `014` duplicates it
- `travel_cost_currency` has one consumer — the review label. If that label is ever dropped, the column goes with it (research D9)
- No background service is introduced anywhere: re-recognition runs inside the request, and `NoMailboxMonitoringTests` still guards that (feature 022)
