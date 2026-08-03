---
description: "Task list for feature implementation"
---

# Tasks: Trip Leg Item Terminology

**Input**: Design documents from `/specs/023-trip-item-terminology/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: Test tasks ARE included. The spec's Edge Cases section states that fixtures and assertions on the old wording "are part of this change, not follow-up work", and research R-005 requires a new guard test for the language-model envelope.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)

## Path Conventions

Repository root holds `src/` and `tests/`. Source projects: `TripPlanner.Api`, `TripPlanner.Web`, `TripPlanner.Contracts`, `TripPlanner.Database`. Test projects: `TripPlanner.Api.Tests`, `TripPlanner.Web.Tests`, `TripPlanner.Database.Tests`, `TripPlanner.E2E.Tests`.

## Working Rule

[data-model.md](./data-model.md) is the authoritative rename map. Anything not listed there keeps its current name. Two things are never renamed: the `'event'` item-type value in all its forms, and the unrelated domain-event identifiers (`audit_events`, `AuditEvent`, `source_event_key`, `SourceEventKey`, `notifications_recipient_event_uq`).

---

## Phase 1: Setup

**Purpose**: Establish the baseline this rename must reproduce exactly.

- [X] T001 Record the pre-change baseline: run `dotnet build TripPlanner.slnx` and each of the three test projects separately, and note the pass/skip counts. FR-012 requires these to be unchanged at the end. **Baseline: build 0 errors / 261 warnings; Api.Tests 102 passed, 6 skipped; Web.Tests 128 passed, 3 skipped; Database.Tests 2 passed, 17 skipped.**
- [ ] T002 Prepare a database that contains data created before the change — at least one trip, one leg, one item typed `event`, and one pending parsed draft. This is the only way to verify the migration in T023 and T051. See [quickstart.md](./quickstart.md) prerequisites.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Rename every *identifier* — database objects, contract types, repository members, recognizer types, enum values — so the solution compiles under the new vocabulary. Traveler-facing *strings* are left for the user story phases.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. This phase is the precondition half of User Story 5 (research R-010).

### Database schema

- [X] T003 [P] Rename `src/TripPlanner.Database/Scripts/Schema/005_trip_leg_events.sql` to `005_trip_leg_items.sql` and update its header comment. File names carry no persistence meaning (research R-002).
- [X] T004 [P] Rename `src/TripPlanner.Database/Scripts/Schema/006_event_detail_shortcuts.sql` to `006_item_detail_shortcuts.sql` and update its header comment ("event-level start/end local times" → "item-level").
- [X] T005 Edit `src/TripPlanner.Database/Scripts/Schema/010_email_ingestion.sql` in place: rename table `parsed_event_drafts` → `parsed_item_drafts`, column `parsed_event_draft_id` → `parsed_item_draft_id`, column `event_type` → `item_type`, constraint `parsed_event_drafts_review_status_chk` → `parsed_item_drafts_review_status_chk`, index `parsed_event_drafts_user_pending_idx` → `parsed_item_drafts_user_pending_idx`, and the header comments. Do NOT rename the `event_type` column in any other table.
- [X] T006 Create `src/TripPlanner.Database/Scripts/Schema/012_parsed_item_drafts_rename.sql` — a guarded `DO $$ ... $$` block that, only when a legacy `parsed_event_drafts` table exists, `INSERT ... SELECT`s its rows into `parsed_item_drafts` with `ON CONFLICT (parsed_item_draft_id) DO NOTHING`, then drops the legacy table. Must be a no-op on fresh databases and on every restart after the first. See research R-003 for why `ALTER TABLE ... RENAME` cannot be used here.

### Database SQL commands and queries

- [X] T007 [P] Rename the three files in `src/TripPlanner.Database/Scripts/Commands/EmailIngestion/` — `InsertParsedEventDraft.sql`, `UpdateParsedEventDraft.sql`, `UpdateParsedEventDraftReviewStatus.sql` → `InsertParsedItemDraft.sql`, `UpdateParsedItemDraft.sql`, `UpdateParsedItemDraftReviewStatus.sql` — and update the table, column, and parameter names inside each.
- [X] T008 [P] Rename the two files in `src/TripPlanner.Database/Scripts/Queries/EmailIngestion/` — `GetParsedEventDraftById.sql`, `GetParsedEventDrafts.sql` → `GetParsedItemDraftById.sql`, `GetParsedItemDrafts.sql` — updating both the column references and the `AS` aliases, which must keep matching the renamed C# record properties.

### Contracts

- [X] T009 Rename the records and properties in `src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs` per [data-model.md](./data-model.md): `ParsedEventDraftDto` → `ParsedItemDraftDto` (with `ParsedEventDraftId` → `ParsedItemDraftId` and `EventType` → `ItemType`), `UpdateParsedEventDraftRequest` → `UpdateParsedItemDraftRequest`, `ConfirmParsedEventDraftResponse` → `ConfirmParsedItemDraftResponse`, `ParsedEventDraftListResponse` → `ParsedItemDraftListResponse`. Leave `TrackedItemTypes.Event` in `TripItems/TripItemContracts.cs` untouched.

### Database repository

- [X] T010 Rename `src/TripPlanner.Database/EmailIngestion/IParsedEventDraftRepository.cs` to `IParsedItemDraftRepository.cs`, rename the interface to `IParsedItemDraftRepository`, and rename its `parsedEventDraftId` parameters and the `NewParsedEventDraft` / `DraftUpdate` member names per the rename map.
- [X] T011 Rename `src/TripPlanner.Database/EmailIngestion/ParsedEventDraftRepository.cs` to `ParsedItemDraftRepository.cs`, rename the class, `ParsedEventDraftRecord` → `ParsedItemDraftRecord`, `NewParsedEventDraft` → `NewParsedItemDraft`, and the private `DraftRow.ParsedEventDraftId`. **Update every `_sql.Get("...")` literal to the new file paths from T007 and T008** — these are strings, not symbols, and the compiler will not catch a mismatch (research R-004).

### API

- [X] T012 In `src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs`, rename `IEventRecognizer` → `IItemRecognizer`, `RecognizedEvent` → `RecognizedItem` (with `EventType` → `ItemType`), `RecognizedEventEnvelope` → `RecognizedItemEnvelope` (with `Events` → `Items`), **and in the same edit** change the prompt text at lines ~74, ~76, and ~86 from `{"events":[...]}` / `eventType` / "empty events array" to the `items` / `itemType` forms. A mismatch between the prompt and the envelope produces a silently empty result with no exception (research R-005).
- [X] T013 [P] Update the type and property references in `src/TripPlanner.Api/Features/EmailIngestion/EmailIngestionMapping.cs`.
- [X] T014 [P] Update the type references in `src/TripPlanner.Api/Features/EmailIngestion/GetDraftListEndpoint.cs`, `UpdateDraftEndpoint.cs`, and `DiscardDraftEndpoint.cs`.
- [X] T015 In `src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs`, rename `NormalizeEventType` → `NormalizeItemType` and update the response and repository type references. Leave the `TrackedItemTypes.Event` fallback and the `"Imported event"` string alone — the string is handled in T041.
- [X] T016 In `src/TripPlanner.Api/Features/EmailIngestion/RelayMessageProcessor.cs`, update the `IItemRecognizer` and `IParsedItemDraftRepository` references and the `record.ParsedItemDraftId` access. Leave the notification strings and `SourceEventKey` alone.
- [X] T017 Rename the `ItineraryChangeKind` values `TripEventCreated` / `TripEventUpdated` / `TripEventDeleted` → `TripItemCreated` / `TripItemUpdated` / `TripItemDeleted` in `src/TripPlanner.Api/Features/Notifications/ItineraryNotificationService.cs`, and update the three call sites in `src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs`. Leave `SourceEventKey` and the local `eventToken` variable alone.
- [X] T018 Update the DI registrations in `src/TripPlanner.Api/Extensions/WebApplicationBuilderExtensions.cs` to the renamed `IParsedItemDraftRepository` / `ParsedItemDraftRepository` and `IItemRecognizer`.

### Web (identifiers only)

- [X] T019 Update the type references in `src/TripPlanner.Web/Features/EmailIngestion/EmailIngestionApiClient.cs` (interface and implementation) to the renamed contract records.
- [X] T020 Update the type and property references in `src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor` (`ParsedItemDraftDto`, `draft.ItemType`, `draft.ParsedItemDraftId`). Leave the traveler-facing strings for T042.

### Foundation verification

- [X] T021 Update the test doubles and fixtures so the solution compiles: `tests/TripPlanner.Api.Tests/EmailIngestion/EmailIngestionApiFactory.cs` (`InMemoryParsedItemDraftRepository`, `StubItemRecognizer`), `tests/TripPlanner.Api.Tests/EmailIngestion/DraftReviewEndpointTests.cs`, `tests/TripPlanner.Api.Tests/Notifications/ItineraryNotificationTriggerTests.cs`, and the `StubEmailIngestionApiClient` in `tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs`.
- [X] T022 Add a deserialization guard test in `tests/TripPlanner.Api.Tests/EmailIngestion/` that feeds a literal `{"items":[{"itemType":"hotel","title":"..."}]}` payload through the envelope type and asserts a non-empty result. This is the regression guard for the highest-risk edit in the feature (research R-005).
- [X] T023 Run `dotnet clean TripPlanner.slnx` then `dotnet build TripPlanner.slnx`, then run each test project separately. The clean step is required — renamed `.sql` files leave stale copies in `bin/` that mask a broken `_sql.Get` lookup (research R-004).
- [ ] T024 Start the app against the T002 database and verify the migration: `parsed_event_drafts` is gone, `parsed_item_drafts` holds the original row count, and a second restart changes nothing. **BLOCKED on T002 — requires a running PostgreSQL instance with pre-change data.**

**Checkpoint**: The solution compiles, tests pass, and data survives. Traveler-facing text is still the old wording — that is expected. User story phases can now begin.

---

## Phase 3: User Story 1 - Planning surfaces call leg children "items" (Priority: P1) 🎯 MVP

**Goal**: Every label, button, count, and empty state on the timeline, trip details, printable trip, and map surfaces calls leg children "items".

**Independent Test**: Open a trip with a mix of item types. Confirm the leg summary reads "2 items", the add control reads "+ Add item" with a matching accessible name, empty states refer to items, the unassigned notice reads grammatically at one and many, and the printable view uses "items" throughout.

### Implementation for User Story 1

- [X] T025 [P] [US1] In `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`: rename `EventCountLabel` → `ItemCountLabel` and change its three literals to "0 items" / "1 item" / "{n} items"; rename `AddEventToLegAsync` → `AddItemToLegAsync`; replace the `@Count event(s) are not related to a trip leg.` placeholder at line ~32 with correct singular and plural forms including verb agreement ("1 item is…" / "N items are…"); update the "+ Add event" label, the `title` and `aria-label` "Add an event to @leg.Title", the "Unassigned event — select to relate it to a trip leg" tooltip, and the "…then relate events to it." empty state. Leave every `EventCallback` and `MouseEventArgs` alone.
- [X] T026 [P] [US1] In `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`: rename `HandleMapOpenEventAsync` → `HandleMapOpenItemAsync` and its `OnOpenEvent` binding; change the "Events" summary label to "Items", the grid help text ("…the events on each leg. Select a leg or event to edit it."), the map tooltip ("Add a location to an event to view the map"), the date-change warning ("…legs or events outside the trip range…"), and the delete confirmation ("…all of its legs, events, and shares."). Leave the modal titles for T036.
- [X] T027 [P] [US1] In `src/TripPlanner.Web/Features/Trips/TripPrintFormatting.cs`: rename `OrderEventsWithinLeg` → `OrderItemsWithinLeg`, `GroupEventsByLeg` → `GroupItemsByLeg`, `ToPrintableEvent` → `ToPrintableItem`, the `PrintableEvent` record → `PrintableItem`, `PrintableLeg.Events` → `PrintableLeg.Items`, and `UnassignedEvents` → `UnassignedItems`, plus the doc comments.
- [X] T028 [US1] In `src/TripPlanner.Web/Components/Trips/TripPrintDocument.razor`: update the renamed members from T027, rename `RenderEventRow` → `RenderItemRow`, change the CSS class `tp-print-event` → `tp-print-item`, and change the strings "This trip has no legs or events yet." and "No events for this leg." to their item forms. Depends on T027.
- [X] T029 [P] [US1] Rename the `.tp-print-event` selector to `.tp-print-item` in `src/TripPlanner.Web/wwwroot/css/app.css`.
- [X] T030 [P] [US1] In `src/TripPlanner.Web/Components/Trips/TripMapModal.razor`: rename the `OnOpenEvent` parameter → `OnOpenItem` and change the empty state "Add an address to an event, or check back once locations can be resolved."
- [X] T031 [P] [US1] Change the map-provider help text in `src/TripPlanner.Web/Components/Pages/Profile.razor` ("Used when you open an event location on a map.") and the matching doc comment in `src/TripPlanner.Contracts/Profile/UserProfileContracts.cs`.

### Tests for User Story 1

- [X] T032 [US1] Update the count assertions and test method names in `tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs` ("0 events" → "0 items", "1 event" → "1 item", "3 events" → "3 items", `LegRow_WithNoEvents_*`, `AddEventButton_*`, `OverlappingEvents_*`), and the names in `tests/TripPlanner.Web.Tests/Timeline/TripTimelineCssTests.cs`.
- [X] T033 [US1] Update `tests/TripPlanner.Web.Tests/Trips/TripPrintDocumentTests.cs` (`tr.tp-print-item` selectors, `RendersItemRowsWith…`), `TripPrintFormattingTests.cs` (`GroupItemsByLeg_PartitionsAndOrders`, `printedLeg.Items`), `TripPrintPageTests.cs` ("no legs or items"), and `TripMapModalTests.cs` (`OnOpenItem`, `OnMarkerActivated_InvokesOpenItemCallback`).

**Checkpoint**: The highest-traffic screens read correctly. This is a shippable increment on its own.

---

## Phase 4: User Story 2 - Adding and editing says "item", and "Event" means only the type (Priority: P2)

**Goal**: The add/edit form and every validation message it can produce call the record an item, while "Event" survives as one of four type choices and as the saved type badge.

**Independent Test**: Open the add form and read the title, labels, and accessible names. Submit with an invalid color, a missing leg, and a start outside the leg window, and read each message. Confirm the type selector still offers "Event" and a saved Event-typed item still shows the "Event" badge.

### Implementation for User Story 2

- [X] T034 [P] [US2] In `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`: change "Add a trip leg first. Every event must be related to a trip leg.", the `[Required]` message "Select the trip leg this event belongs to.", the end-timezone error "Select an end timezone for the event end, or clear the end date.", and `aria-label="Event color"` → `"Item color"`. Update the internal comments that say "event" to "item". **Leave `<option value="event">Event</option>` exactly as it is** (FR-009).
- [X] T035 [P] [US2] In `src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs`, change the messages "Select an end timezone for the event end.", "Select a valid event color.", "Add a trip leg before adding an event, then relate the event to that leg.", and "Select the trip leg this event belongs to." to their item forms, plus the internal comments. **Leave the message "Item type must be one of: event, reservation, activity, reminder." alone** — those are type values.
- [X] T036 [US2] In `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`, change the modal titles "Add event" / "Edit event" → "Add item" / "Edit item", and the comment "Default a new event to the timeline's active (centered) date".
- [X] T037 [P] [US2] In `src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs`, change "This trip leg still has related events. Reassign or remove those events before deleting the leg." to its item form.

### Tests for User Story 2

- [X] T038 [US2] Update the test names and asserted strings in `tests/TripPlanner.Web.Tests/TripItems/` — `TrackedItemFormLegWindowTests.cs` (`NewItem_StartOutsideLegWindow_…`), `TrackedItemFormReactiveEndTests.cs` (`EditingExistingItem_…`), `TrackedItemFormDateDefaultTests.cs` and `TrackedItemFormTestData.cs` comments and the "Existing event" fixture title — and `Validator_AcceptsItemOnLegBoundaries` in `tests/TripPlanner.Api.Tests/TripItems/TrackedItemEndpointTests.cs`.
- [X] T039 [US2] Confirm the Event-type regression coverage still passes unchanged: `tests/TripPlanner.Web.Tests/TripItems/TrackedItemIconTests.cs` (`[InlineData("event")]`), `tests/TripPlanner.Web.Tests/Timeline/TripTimelineIconTests.cs`, and `tests/TripPlanner.Web.Tests/Trips/TripFixtures.cs` (`TrackedItemTypes.Event`). These files' type data must NOT change (FR-016).

**Checkpoint**: The form no longer calls a reservation an event, and the type vocabulary is intact.

---

## Phase 5: User Story 3 - Notifications and email-review say "item" (Priority: P3)

**Goal**: Change notifications to collaborators and the parsed-email review surfaces describe items.

**Independent Test**: On a shared trip, add, edit, and delete an item as a collaborator and read all three notifications. Open the review screen with a pending draft, with no drafts, and with a draft that has no trip assigned.

### Implementation for User Story 3

- [X] T040 [P] [US3] In `src/TripPlanner.Api/Features/Notifications/ItineraryNotificationService.cs`, change the three message strings to "added a new item to the trip", "updated an item on the trip", "removed an item from the trip".
- [X] T041 [P] [US3] In `src/TripPlanner.Api/Features/EmailIngestion/RelayMessageProcessor.cs`, change "New trip event ready to review" → "New trip item ready to review", "{n} trip events ready to review" → "{n} trip items ready to review", "A relayed email was processed. Review and confirm the extracted events." → "…extracted items.", and "No trip event could be recognized." → "No trip item could be recognized." Leave `SourceEventKey` alone.
- [X] T042 [P] [US3] In `src/TripPlanner.Api/Features/EmailIngestion/ConfirmDraftEndpoint.cs`, change the fallback title `"Imported event"` → `"Imported item"`.
- [X] T043 [P] [US3] In `src/TripPlanner.Web/Components/Pages/EmailIngestion/InboxDrafts.razor`, change the `<PageTitle>` and `<h1>` "Review parsed events" → "Review parsed items", the empty state "No events are waiting for review…" → "No items are waiting for review…", the untitled fallback "Untitled event" → "Untitled item", the guidance "Assign this event to a trip before confirming." → "…this item…", and the load error "We couldn't load your draft events." → "…draft items."

### Tests for User Story 3

- [X] T044 [US3] Update the asserted strings and test names in `tests/TripPlanner.Web.Tests/EmailIngestion/InboxReviewPageTests.cs` ("No items are waiting for review", "Assign this item to a trip before confirming.", `DraftsPageListsPendingItemsForReview`, `OnlyMessagesThatDidNotYieldItemsOfferReprocessing`) and the name `IngestionNeverCreatesATripItem` in `tests/TripPlanner.Api.Tests/EmailIngestion/RelayIngestionEndpointTests.cs`.

**Checkpoint**: Out-of-app surfaces match in-app wording.

---

## Phase 6: User Story 4 - Help and informational content is consistent (Priority: P4)

**Goal**: FAQ and About describe trip structure with the same vocabulary as the planning screens.

**Independent Test**: Read the FAQ and About pages end to end and confirm every reference to leg children says "item" and the four types are named accurately.

### Implementation for User Story 4

- [X] T045 [P] [US4] In `src/TripPlanner.Web/Components/Pages/Faq.razor`, update the five answers listed in [data-model.md](./data-model.md): the Viewer/Collaborator answer ("such as legs and events"), the time zone question and answer, the date validation answer, the timeline answer, and the deletion answer.
- [X] T046 [P] [US4] Review `src/TripPlanner.Web/Components/Pages/About.razor` and align any description of leg children to "item". A pre-change search showed no "event" hits in this file, so verify rather than assume a change is needed.
- [X] T047 [US4] Update any assertion in `tests/TripPlanner.Web.Tests/` that references FAQ or About copy changed by T045 or T046.

**Checkpoint**: All four traveler-facing stories are complete.

---

## Phase 7: User Story 5 - One vocabulary from screen to database (Priority: P5)

**Goal**: Close out the rename and prove no generic use of "event" survives. The identifier half of this story landed in Phase 2; this phase is the verification gate.

**Independent Test**: Run the terminology gate and confirm every surviving hit falls into one of its three allowed categories, with the application starting, reading pre-existing data, and passing its full suite.

- [X] T048 [US5] Run the search command in [contracts/terminology-gate.md](./contracts/terminology-gate.md) over `src/`, `tests/`, and `infra/`. Triage every hit into: the Event type value, unrelated domain events, or framework/DOM plumbing. Fix anything that falls outside those three.
- [X] T049 [US5] Run the plural-forms check for `item(s)` and `event(s)`. Expect zero results.
- [X] T050 [US5] Run `dotnet clean TripPlanner.slnx`, `dotnet build TripPlanner.slnx`, then each of the three test projects separately. Compare against the T001 baseline — pass and skip counts must match (SC-005).
- [ ] T051 [US5] Re-verify the migration against the T002 database and confirm items typed `event` before the change still load and display as Event (SC-006, FR-016).
- [ ] T052 [US5] Smoke-test the email path end to end: forward or simulate a booking email, confirm a draft appears with a populated type and title, and confirm it into a trip. An empty review queue with no error is the silent failure mode this checks for (research R-005). **Partially covered by T022's automated envelope tests; the live end-to-end pass still requires a running app.**

**Checkpoint**: The rename is complete and provably so.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T053 [P] Rename the E2E placeholder test methods in `tests/TripPlanner.E2E.Tests/` — `AddEventFromLegRow_*`, `AddingEvent_UpdatesOnlySelectedLegEventCount`, `AddEvent_DefaultsStartToActiveDate_*`, `SelectingMarker_OpensEventDetails`, `OwnerClicksPrint_OpensChromeFreePrintPageWithLegsAndEvents` — and their file header comments. These are empty stubs; implementing them is out of scope.
- [X] T054 [P] Rename the remaining placeholder test names `GetTimeline_ReturnsOwnerScopedEvents` in `tests/TripPlanner.Api.Tests/Timeline/TimelineEndpointTests.cs` and `Timeline_OnlyIncludesOwnerScopedEvents` in `tests/TripPlanner.Database.Tests/Timeline/TimelineQueryTests.cs`.
- [X] T055 [P] Update the remaining internal comments that use "event" generically: `src/TripPlanner.Api/Security/TripAccessResolver.cs` ("legs/events"), `src/TripPlanner.Api/Features/TripMaps/GetTripMapEndpoint.cs` ("fan the coordinates back to each event"), `src/TripPlanner.Contracts/Trips/TripMapContracts.cs` ("event location text", "source event's id"), and `src/TripPlanner.Database/Scripts/Schema/009_user_profile_map_provider.sql`.
- [X] T056 Walk through [quickstart.md](./quickstart.md) end to end as a final acceptance pass.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup. **Blocks every user story.** Unusually heavy for this feature because the identifier rename must land atomically for the solution to compile.
- **US1 (Phase 3)**, **US2 (Phase 4)**, **US3 (Phase 5)**, **US4 (Phase 6)**: All depend only on Phase 2. Mutually independent — see the file-overlap note below.
- **US5 (Phase 7)**: Depends on US1 through US4, since the gate cannot pass while any traveler-facing string is unconverted.
- **Polish (Phase 8)**: Can start after Phase 2; best run before T048 so the gate sees a clean tree.

### Within Phase 2

- T005 must precede T006 (the reconciling script targets the table `010` now creates).
- T007 and T008 must precede T011 (the `_sql.Get` strings must match the new file names).
- T009 must precede T010, T011, T013–T016, T019, T020.
- T012 is self-contained but must be a single atomic edit.
- T021 must follow T009–T020. T023 and T024 must follow everything else in the phase.

### Cross-story file overlap

`TripDetails.razor` is touched by both T026 (US1) and T036 (US2). Those two tasks must not run concurrently. Every other user story task operates on a distinct file.

### Parallel Opportunities

- **Phase 2 schema**: T003, T004 in parallel; then T007, T008 in parallel.
- **Phase 2 API**: T013, T014 in parallel after T009.
- **Phase 3**: T025, T026, T027, T029, T030, T031 all in parallel (T028 waits on T027).
- **Phase 4**: T034, T035, T037 in parallel.
- **Phase 5**: T040, T041, T042, T043 all in parallel.
- **Phase 6**: T045, T046 in parallel.
- **Phase 8**: T053, T054, T055 in parallel.
- **Across stories**: once Phase 2 is done, US1 through US4 can be worked simultaneously by different people, with the one `TripDetails.razor` handoff noted above.

---

## Implementation Strategy

### MVP scope

**Phase 1 + Phase 2 + Phase 3 (US1)**. That delivers correct vocabulary on the timeline, trip details, and printable trip — the screens where the collision is most visible — on top of a fully renamed data and code layer.

Phase 2 cannot be trimmed. It is not optional infrastructure; without it the solution does not compile under any of the new names.

### Incremental delivery

Each of Phases 3 through 6 is independently shippable and independently demonstrable. Phase 7 is the closeout gate and should not be attempted until all four have landed, because the gate is all-or-nothing by design.

### Risk order

The two highest-risk tasks are **T006** (migration correctness on pre-existing databases) and **T012** (prompt and envelope must change together or parsing silently returns nothing). Both land early in Phase 2 with dedicated verification — T024 for the migration, T022 for the envelope — so a mistake surfaces immediately rather than at the end.
