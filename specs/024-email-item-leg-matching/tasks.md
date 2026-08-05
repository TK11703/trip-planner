# Tasks: Matching Ingested Email Items to Trip Legs

**Input**: Design documents from `/specs/024-email-item-leg-matching/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/email-ingestion-placement.md](contracts/email-ingestion-placement.md), [quickstart.md](quickstart.md)

**Tests**: Included. The spec's user stories each carry an Independent Test, [plan.md](plan.md) names specific new and modified test files, and [quickstart.md](quickstart.md) lists twelve contract test obligations.

**Organization**: Tasks are grouped by user story so each can be implemented and verified independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: The user story this task serves (US1–US4)
- Every task names the exact file it touches

## Path Conventions

Repository root holds `src/` and `tests/`. Paths below are verbatim from [plan.md](plan.md)'s source tree and have been confirmed against the working tree.

---

## Phase 1: Setup

**Purpose**: Establish the branch and the baseline that the breaking contract change will be measured against.

- [X] T001 Capture the pre-change baseline: run `dotnet build TripPlanner.slnx`, then `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj`, `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`, and `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj` separately (a single `dotnet test` call rejects two project paths with MSB1008). Record error count, warning count, and passed/skipped per suite
- [X] T002 Create and switch to branch `024-email-item-leg-matching` — `setup-plan.ps1` reported an empty branch, so the feature has no branch yet

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Make a trip leg optional. Every user story below assumes an item can exist without a leg, so nothing else can start until this lands.

**⚠️ CRITICAL**: `TripLegId` changing from `Guid` to `Guid?` breaks compilation across Contracts, API, Web, and tests. Finish this phase and return to the T001 baseline before starting any story.

- [X] T003 [P] Create `TripInstant` with `ToInstant(DateTime local, TimeZoneInfo zone)` in src/TripPlanner.Contracts/TripItems/TripInstant.cs, lifted verbatim from the private helper in src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs so the validator and the matcher provably share one definition (research.md D5)
- [X] T004 [P] Change `TripLegId` from `Guid` to `Guid?` on `CreateTrackedItemRequest` and `UpdateTrackedItemRequest` in src/TripPlanner.Contracts/TripItems/TripItemContracts.cs
- [X] T005 [P] Change `ConfirmParsedItemDraftResponse.TripLegId` from `Guid` to `Guid?` in src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs
- [X] T006 Relax `ValidateCore` in src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs: accept `Guid? tripLegId`, delete the `trip.Legs.Count == 0` rejection and the `tripLegId == Guid.Empty` rejection, keep the leg-not-on-this-trip rejection and the start/end window checks but run them only when a leg is supplied, and delegate to `TripInstant.ToInstant` (FR-011, FR-014, FR-020, FR-021)
- [X] T007 Update src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs to compile against the nullable contracts — pass `draft.TripLegId` straight through and return it in the response. Leave the existing "must have a TripLegId" guard in place for now; US2 removes it
- [X] T008 Update src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor to build the create and update requests with a nullable `TripLegId`, and relabel the leg `InputSelect` empty option from "Select a trip leg" to "No trip leg yet" so an unassigned save is reachable from the form (FR-021)
- [X] T009 Verify no change is needed in src/TripPlanner.Database/TripItems/TripItemRepository.cs — Dapper maps a null `Guid?` to SQL NULL, `tracked_items.trip_leg_id` is already nullable per src/TripPlanner.Database/Scripts/Schema/005_trip_leg_items.sql, and src/TripPlanner.Database/Scripts/Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql already guards with `@TripLegId IS NULL OR EXISTS (...)`. Record the finding; do not edit unless it proves otherwise
- [X] T010 [P] Verify src/TripPlanner.Web/Components/Timeline/TripTimeline.razor renders a leg-less item in its existing `UnassignedItems` lane without error
- [X] T011 [P] Verify src/TripPlanner.Web/Components/Trips/TripPrintDocument.razor handles an item with no leg rather than dropping it or throwing while grouping by leg
- [X] T012 [P] Verify src/TripPlanner.Web/Components/Trips/TripMapModal.razor handles an item with no leg
- [X] T013 Replace `Validator_RejectsMissingLeg_WhenTripHasNoLegs` in tests/TripPlanner.Api.Tests/TripItems/TrackedItemEndpointTests.cs with a test asserting the same input now validates, and update the remaining `CreateTrackedItemRequest` constructions for the nullable parameter. Keep `Validator_RejectsLegFromDifferentTrip` asserting `field == "tripLegId"`
- [X] T014 [P] Update tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormLegWindowTests.cs so the window assertions still hold when a leg is chosen and are skipped when none is
- [X] T015 [P] Update the `Item` factory and stub client in tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormTestData.cs for the nullable leg parameter
- [X] T016 Rebuild and re-run all three suites; confirm parity with the T001 baseline before proceeding

**Checkpoint**: A leg is optional, its window is binding when present, and the suite is green. User stories can begin.

---

## Phase 3: User Story 1 - A Forwarded Confirmation Lands on the Right Leg (Priority: P1) 🎯 MVP

**Goal**: A draft whose dates fall inside exactly one editable leg arrives in the review queue with that trip and leg already selected, so confirming is the traveler's only remaining action.

**Independent Test**: Ingest a confirmation dated inside exactly one existing leg, open the review queue, verify the proposed trip and leg are correct and that confirming produces a timeline item on that leg carrying the recognized details.

### Tests for User Story 1

> Write these first and watch them fail.

- [X] T017 [P] [US1] Create tests/TripPlanner.Api.Tests/EmailIngestion/DraftPlacementMatcherTests.cs covering: exactly one containing leg reports `Matched` with both suggested ids (FR-004); two containing legs report `Ambiguous` with both candidates and no suggestion (FR-005); a draft with no start reports `InsufficientData`; a leg with a null `end_at` is treated as open-ended; containment is judged on instants so a Tokyo-zoned draft matches a Pacific-zoned leg that contains it in real time (FR-002); a viewer-level trip contributes no candidates (FR-003)
- [X] T018 [P] [US1] Add assertions to tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs that `GET /drafts` returns a `placement` object per draft and that evaluating it creates, alters, and deletes nothing (FR-006)
- [X] T019 [P] [US1] Add a bUnit test to tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs asserting a `Matched` draft renders with its trip and leg pre-selected and shows the matched dates as the reason (FR-007)

### Implementation for User Story 1

- [X] T020 [US1] Add `DraftPlacementStatus`, `PlacementCandidate`, and `DraftPlacement` records and a `Placement` property on `ParsedItemDraftDto` in src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs, per contracts/email-ingestion-placement.md
- [X] T021 [P] [US1] Create src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetPlacementCandidateLegs.sql returning every leg on every trip the caller can edit, reusing the owner-plus-shares CTE from src/TripPlanner.Database/Scripts/Queries/Trips/GetTripsPage.sql filtered to Owner and Collaborator access, with parameters `@OwnerUserId` and `@CallerEmail`
- [X] T022 [US1] Add a `PlacementCandidateLeg` record and `GetPlacementCandidateLegsAsync` to src/TripPlanner.Database/EmailIngestion/IParsedItemDraftRepository.cs and implement it in src/TripPlanner.Database/EmailIngestion/ParsedItemDraftRepository.cs using `_sql.Get("Queries/EmailIngestion/GetPlacementCandidateLegs.sql")`
- [X] T023 [US1] Implement the new repository member on the in-memory double in tests/TripPlanner.Api.Tests/EmailIngestion/EmailIngestionApiFactory.cs
- [X] T024 [US1] Create src/TripPlanner.Api/Features/EmailIngestion/DraftPlacementMatcher.cs: compare each draft's start and end as instants against each candidate leg's window using `TripInstant.ToInstant`, and return `Matched`, `Ambiguous`, `InsufficientData`, or `NoLegCovers`. Persist nothing (FR-001, FR-002, FR-006)
- [X] T025 [US1] Register `DraftPlacementMatcher` alongside the other ingestion services in src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs
- [X] T026 [US1] Map `DraftPlacement` onto `ParsedItemDraftDto` in src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionMapping.cs
- [X] T027 [US1] In src/TripPlanner.Api/Features/EmailIngestion/GetDraftListEndpoint.cs fetch candidate legs once per request and attach a placement to every draft, so no extra round trip is added per draft (SC-008)
- [X] T028 [US1] In src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor render the proposed trip and leg pre-selected for `Matched`, list all candidates unselected for `Ambiguous`, and show the matched dates as the stated reason (FR-004, FR-005, FR-007)
- [X] T029 [US1] In src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor show an explicit "a start date and time zone are required" state for `InsufficientData` rather than a blank proposal (US1 scenario 6, SC-003)

**Checkpoint**: A forwarded confirmation reaches a placed timeline item in one confirming action.

---

## Phase 4: User Story 2 - No Leg Covers the Item's Timeframe (Priority: P2)

**Goal**: A booking that falls in a gap, or arrives before its leg exists, can still be captured onto the trip — unassigned, visible, and relatable later.

**Independent Test**: Ingest a confirmation dated outside every leg of the traveler's only trip, confirm it without a leg, and verify it appears in the timeline's unassigned area with its dates intact and the draft cleared from the queue.

**Dependency note**: The API behavior below is independently testable. Exercising the full UI path needs the trip picker from US3, so build US3 first as [plan.md](plan.md) sequences it.

### Tests for User Story 2

- [X] T030 [P] [US2] Add cases to tests/TripPlanner.Api.Tests/EmailIngestion/DraftPlacementMatcherTests.cs: a draft between two legs but inside the trip's own dates reports `NoLegCovers`; a draft outside every trip's date range reports `OutsideTripDates`; a trip with zero legs reports `NoLegCovers` rather than an error (FR-009, FR-010, FR-014)
- [X] T031 [P] [US2] Add cases to tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs: confirming a draft with a trip and no leg returns 200 and creates an item with a null `trip_leg_id`; confirming with no trip returns 400 (FR-011)
- [X] T032 [P] [US2] Add bUnit cases to tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs: the two gap statuses render distinct messages naming the dates, and Confirm is enabled once a trip is chosen even with no leg

### Implementation for User Story 2

- [X] T033 [US2] Extend src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetPlacementCandidateLegs.sql to also return each trip's own start and end dates, so a gap can be told apart from a draft outside the trip entirely
- [X] T034 [US2] In src/TripPlanner.Api/Features/EmailIngestion/DraftPlacementMatcher.cs split the non-match outcome into `NoLegCovers` and `OutsideTripDates` using the trip date range, and route a trip with no legs down the `NoLegCovers` path (FR-009, FR-010, FR-014)
- [X] T035 [US2] In src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs remove the `draft.TripLegId is null` guard, keep `TripId` required, and return the nullable leg id in `ConfirmParsedItemDraftResponse` (FR-011)
- [X] T036 [US2] In src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor render a distinct message for each gap status naming the dates in question, and enable Confirm when a trip is selected and no leg is (FR-009, FR-010, FR-011, SC-004)
- [X] T037 [P] [US2] Add a test to tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs asserting an item with no leg appears in the unassigned lane showing its dates and reading as awaiting a leg (FR-012, SC-005)
- [X] T038 [P] [US2] Add tests to tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormLegWindowTests.cs asserting an unassigned item can be related to a covering leg and is refused against a non-covering one (FR-013)
- [ ] T039 [US2] Walk quickstart.md Scenarios 3, 4, and 5 against a running app and record the outcomes

**Checkpoint**: A booking outside every leg is capturable without touching the itinerary.

---

## Phase 5: User Story 3 - Correcting a Wrong Placement Before Confirming (Priority: P3)

**Goal**: The traveler can open a draft, pick the trip and leg themselves, and clean up any parsed field before saving.

**Independent Test**: Ingest a draft, change its trip and leg away from the proposal, confirm, and verify the item lands where the traveler chose rather than where the system suggested.

### Tests for User Story 3

- [X] T040 [P] [US3] Add bUnit tests to tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs: the edit modal opens from a draft; changing the trip repopulates the leg list and clears any prior leg choice; a viewer-level trip is absent from the picker; saving persists the edited fields (FR-016, FR-017, FR-018, US3 scenario 4)
- [X] T041 [P] [US3] Add tests to tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs: `PUT /drafts/{id}` with a leg belonging to another trip returns 400 with `field == "tripLegId"`; a successful update returns a placement recomputed against the new dates (FR-008, FR-018)

### Implementation for User Story 3

- [X] T042 [US3] In src/TripPlanner.Api/Features/EmailIngestion/UpdateDraftEndpoint.cs reject a `TripLegId` that does not belong to the supplied `TripId` and a `TripLegId` supplied without a `TripId`, returning 400 naming `tripLegId`, and return a recomputed placement on the updated DTO (FR-008, FR-018)
- [X] T043 [US3] Create src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor with the markup and `[Parameter] public EventCallback OnClose` pattern used by src/TripPlanner.Web/Components/Trips/ShareTripModal.razor
- [X] T044 [US3] In src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor add a trip `InputSelect` populated from `ITripApiClient.GetTripsAsync` filtered to Owner and Collaborator access (FR-003, FR-016)
- [X] T045 [US3] In src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor add a leg `InputSelect` repopulated from `ITripApiClient.GetDetailAsync` whenever the trip changes, offering "No trip leg yet" and clearing any leg carried over from the previous trip (FR-011, FR-018)
- [X] T046 [US3] In src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor add the remaining editable fields — item type, title, location, start and end local with their time zones, confirmation code, notes — using `EditForm` with `DataAnnotationsValidator` and an `IValidatableObject` model mirroring src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor (FR-017)
- [X] T047 [US3] In src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor save through the existing `IEmailIngestionApiClient.UpdateDraftAsync`, surfacing a 400 against the field it names
- [X] T048 [US3] In src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor add an Edit action per draft, host `DraftEditModal`, and reload the queue on save so the placement refreshes
- [X] T049 [US3] Extend the test doubles in tests/TripPlanner.Web.Tests/Infrastructure/StubTripApiClient.cs to serve `GetTripsAsync` and `GetDetailAsync` for the modal
- [ ] T050 [US3] Walk quickstart.md Scenario 6 against a running app and record the outcome

**Checkpoint**: A wrong proposal is correctable in place, without leaving the review queue.

---

## Phase 6: User Story 4 - Email Items Obey the Same Rules as Typed Items (Priority: P4)

**Goal**: Confirmation runs the same validator the item form runs, invents nothing, notifies collaborators identically, and records which item each draft became.

**Independent Test**: Attempt, through the email path, each placement the item form refuses, and verify the email path refuses it too with comparable wording and no item written.

### Tests for User Story 4

- [X] T051 [P] [US4] Add tests to tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs: confirming a draft assigned to a leg that does not contain it returns 400 naming `startLocal` or `endLocal` and writes no item (FR-020, SC-006); confirming a draft missing a required detail returns 400 naming that detail (FR-023); a successful confirm records `tracked_item_id` (FR-025) and raises the same itinerary notification a manual add raises (FR-024)
- [X] T052 [P] [US4] Add tests to tests/TripPlanner.Api.Tests/TripItems/TrackedItemEndpointTests.cs asserting parity: an item saves with a null leg, an out-of-window leg is still refused, and a leg from another trip is still refused (FR-020, FR-021)

### Implementation for User Story 4

- [X] T053 [P] [US4] Create src/TripPlanner.Database/Scripts/Schema/013_draft_item_traceability.sql adding `tracked_item_id uuid NULL REFERENCES tracked_items(tracked_item_id) ON DELETE SET NULL` to `parsed_item_drafts` via `ADD COLUMN IF NOT EXISTS`, safe to replay on every start
- [X] T054 [US4] Update src/TripPlanner.Database/Scripts/Commands/EmailIngestion/UpdateParsedItemDraftReviewStatus.sql to also write `tracked_item_id`
- [X] T055 [US4] Add `TrackedItemId` to `ParsedItemDraftRecord` and a `Guid? trackedItemId` parameter to `SetReviewStatusAsync` in src/TripPlanner.Database/EmailIngestion/IParsedItemDraftRepository.cs, and implement it in src/TripPlanner.Database/EmailIngestion/ParsedItemDraftRepository.cs
- [X] T056 [US4] Select `tracked_item_id` in src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetParsedItemDrafts.sql and src/TripPlanner.Database/Scripts/Queries/EmailIngestion/GetParsedItemDraftById.sql
- [X] T057 [US4] In src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs resolve trip access, load the `TripDetail` with its legs, build the `CreateTrackedItemRequest`, and run `TrackedItemValidator` before creating anything — returning the validator's own field-level error on failure (FR-019, FR-020, FR-022)
- [X] T058 [US4] In src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs stop substituting `"Imported item"` for a missing title and `"UTC"` for a missing start time zone; return 400 naming the missing detail instead (FR-023)
- [X] T059 [US4] In src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs record the created item id on the draft, write an audit entry, and raise `IItineraryNotificationService.NotifyChangeAsync` with `ItineraryChangeKind.TripItemCreated` exactly as src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs does (FR-024, FR-025)
- [X] T060 [US4] Update the in-memory draft repository in tests/TripPlanner.Api.Tests/EmailIngestion/EmailIngestionApiFactory.cs for the new `SetReviewStatusAsync` signature and the `TrackedItemId` field
- [X] T061 [US4] Surface a rejected confirmation against the offending field in src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor and src/TripPlanner.Web/Components/EmailIngestion/DraftEditModal.razor so the traveler can correct it without leaving the queue (FR-022)
- [ ] T062 [US4] Walk quickstart.md Scenarios 7, 10, and 11 against a running app and record the outcomes

**Checkpoint**: No email-created item exists that the traveler could not have typed by hand.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T063 [P] Start the AppHost twice against the same database and confirm src/TripPlanner.Database/Scripts/Schema/013_draft_item_traceability.sql replays without error, since `DatabaseInitializer` runs every schema script on every start
- [X] T064 [P] Add a round-trip test for `tracked_item_id` to tests/TripPlanner.Database.Tests/ alongside the existing draft coverage
- [X] T065 [P] Confirm tests/TripPlanner.Api.Tests/EmailIngestion/NoMailboxMonitoringTests.cs still passes — matching must run inside a request, never in a background service
- [X] T066 [P] Remove the now-dead validation strings and any unused usings left behind in src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs and src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs
- [X] T067 Re-run `dotnet build TripPlanner.slnx` and all three test suites separately; compare error, warning, and pass counts against the T001 baseline
- [X] T068 Complete the exit criteria checklist at the end of quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Setup — **blocks every user story**
- **US1 (Phase 3)**: Depends on Foundational
- **US2 (Phase 4)**: Depends on Foundational and on US1's matcher (T024) and query (T021), which it extends
- **US3 (Phase 5)**: Depends on Foundational. Independent of US2 in code; needed by US2's UI path in practice
- **US4 (Phase 6)**: Depends on Foundational. T057 assumes US2's relaxed confirm guard (T035)
- **Polish (Phase 7)**: Depends on all stories

### Recommended build order

Foundation → US1 → **US3** → US2 → US4, matching the delivery slices in [plan.md](plan.md). US3 comes before US2 because US2's traveler-facing path — "assign the draft to the trip, leave the leg empty, confirm" — needs the trip picker US3 builds. US2's API behavior remains independently testable without it.

### Within each user story

- Tests are written first and must fail before implementation
- Contracts before repositories, repositories before the matcher, matcher before endpoints, endpoints before UI
- A story is complete before the next begins

### Sequential constraints worth naming

- T028, T029, T036, T048, and T061 all edit `InboxDrafts.razor` — never run them concurrently
- T043 through T047 build `DraftEditModal.razor` in sequence
- T021 and T033 both edit `GetPlacementCandidateLegs.sql`; T024 and T034 both edit `DraftPlacementMatcher.cs`
- T004 and T006 must precede every other compile-affecting task

### Parallel Opportunities

- Phase 2: T003, T004, T005 together; then T010, T011, T012 together; then T014, T015 together
- Phase 3: T017, T018, T019 together; T021 alongside T020
- Phase 4: T030, T031, T032 together; later T037 and T038 together
- Phase 5: T040 and T041 together
- Phase 6: T051 and T052 together; T053 alongside them
- Phase 7: T063, T064, T065, T066 together

---

## Implementation Strategy

### MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)** — 29 tasks. This delivers the whole point of ingestion: a forwarded confirmation whose dates fall inside one leg arrives pre-placed and confirms in one action (SC-001, SC-002). It is shippable on its own; drafts that do not match simply stay pending, exactly as they do today.

### Incremental delivery

1. **Foundation** — a leg becomes optional. Nothing user-visible, but every later slice rests on it. Verify against the T001 baseline before moving on
2. **US1** — placement proposals appear. Ship here if you need to ship early
3. **US3** — the inbox gains a real editor, which also unblocks US2's UI
4. **US2** — the reported failure is fixed: bookings ahead of the itinerary get captured
5. **US4** — the correctness guarantee. Closes the pre-existing hole where confirm bypassed validation entirely

### Independent test criteria

| Story | Verified by |
| ----- | ----------- |
| US1 (P1) | A draft inside exactly one leg reports that leg as its sole candidate and confirms in one action — `DraftPlacementMatcherTests`, `InboxReviewPageTests` |
| US2 (P2) | A draft outside every leg confirms to an unassigned item that shows on the timeline — `DraftReviewEndpointTests`, `TripTimelineTests` |
| US3 (P3) | Changing a draft's trip repopulates the leg list and the edits persist — `InboxReviewPageTests`, `DraftReviewEndpointTests` |
| US4 (P4) | An out-of-window confirmation is refused and writes nothing; a confirmed draft records the item it became — `DraftReviewEndpointTests`, `TrackedItemEndpointTests` |

### Task counts

| Phase | Tasks |
| ----- | ----- |
| Setup | 2 |
| Foundational | 14 |
| US1 (P1) | 13 |
| US2 (P2) | 10 |
| US3 (P3) | 11 |
| US4 (P4) | 12 |
| Polish | 6 |
| **Total** | **68** |
