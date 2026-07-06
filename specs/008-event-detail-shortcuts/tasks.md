# Tasks: Event Detail Fields and Quick-Fill Shortcuts

**Input**: Design documents from `specs/008-event-detail-shortcuts/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md), [quickstart.md](./quickstart.md)

**Tests**: No test-first tasks are included because the specification does not request TDD. Validation tasks at the end run the focused commands from [quickstart.md](./quickstart.md).

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files or has no dependency on incomplete tasks.
- **[Story]**: Maps implementation tasks to the user story they deliver.
- Every task includes exact file paths.

---

## Phase 1: Setup (Shared Contract and Storage Shape)

**Purpose**: Establish the shared data and contract shape used by all stories.

- [X] T001 Add tracked item local/timezone columns and length constraints for `confirmation_code` and `notes` in `src/TripPlanner.Database/Scripts/Schema/006_event_detail_shortcuts.sql`
- [X] T002 [P] Replace tracked item create/update request timing fields with `StartLocal`, `StartTimeZoneId`, `EndLocal`, and `EndTimeZoneId` in `src/TripPlanner.Contracts/TripItems/TripItemContracts.cs`
- [X] T003 [P] Add tracked item `StartLocal`, `StartTimeZoneId`, `EndLocal`, and `EndTimeZoneId` response fields in `src/TripPlanner.Contracts/Trips/TripContracts.cs`
- [X] T004 [P] Add timeline item local/timezone response fields needed by the custom timeline in `src/TripPlanner.Contracts/Timeline/TimelineContracts.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire the shared schema, SQL, repository, API, and client plumbing before user-facing behavior is implemented.

**Critical**: No user story work should begin until this phase is complete.

- [X] T005 Update tracked item insert/update/select SQL to persist and return local/timezone fields in `src/TripPlanner.Database/Scripts/Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql`
- [X] T006 Update timeline query SQL to return tracked item local/timezone fields without breaking existing ordering in `src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql`
- [X] T007 Derive `StartsAt` and `EndsAt` from local date/time plus timezone IDs in `src/TripPlanner.Database/TripItems/TripItemRepository.cs`
- [X] T008 Map tracked item local/timezone fields into timeline DTOs in `src/TripPlanner.Database/Timeline/TimelineRepository.cs`
- [X] T009 Validate supported timezone IDs, required timezone rules, end-after-start conversion, and length limits in `src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs`
- [X] T010 Update tracked item create/update endpoint request handling and validation error messages in `src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs`
- [X] T011 Update the web API client serialization expectations for tracked item create/update requests in `src/TripPlanner.Web/Features/Trips/TripApiClient.cs`

**Checkpoint**: Contracts, database, API, repository, timeline projection, and web client agree on the tracked item event detail shape.

---

## Phase 3: User Story 1 - Capture Start and End Timezone Selections on an Event (Priority: P1) MVP

**Goal**: Travelers can select and persist a start timezone and, when an end exists, an end timezone for an event.

**Independent Test**: Open an event, select start and end timezones, save, leave and reopen the event, and confirm both timezone selections are retained and applied to the event times.

### Implementation for User Story 1

- [X] T012 [US1] Inject the existing timezone options provider into the event form in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T013 [US1] Add `StartTimeZoneId` and `EndTimeZoneId` fields with required validation rules to the item model in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T014 [US1] Render start and end timezone dropdowns next to the start and end date fields in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T015 [US1] Populate event edit state from `StartLocal`, `StartTimeZoneId`, `EndLocal`, and `EndTimeZoneId` in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T016 [US1] Submit `StartLocal`, `StartTimeZoneId`, `EndLocal`, and `EndTimeZoneId` instead of browser-local `DateTimeOffset` values in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T017 [US1] Preserve timeline rendering after timezone-aware tracked item changes in `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`

**Checkpoint**: User Story 1 is independently functional and can be validated with quickstart scenario 1.

---

## Phase 4: User Story 2 - Edit Existing Event Fields (Priority: P1)

**Goal**: Travelers can edit original event fields plus timezone selections, Confirmation/Reservation Code, and Notes without losing leg relationship or timeline placement.

**Independent Test**: Open an existing event, change timezone selections, confirmation/reservation code, notes, and a timeline-affecting field, save, then reopen and confirm all values persist and the timeline reflects the change.

### Implementation for User Story 2

- [X] T018 [US2] Rename the confirmation label to `Confirmation/Reservation Code` and keep it before Notes in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T019 [US2] Render the missing Notes free-text input after Confirmation/Reservation Code in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T020 [US2] Add 255-character validation for Confirmation/Reservation Code and 2,000-character validation for Notes in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T021 [US2] Ensure create and update submissions include cleared optional Confirmation/Reservation Code and Notes values in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T022 [US2] Keep edit modal cancel/close behavior from persisting pending changes in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`
- [X] T023 [US2] Confirm owner-scoped event edit failures still surface through the existing alert path in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`

**Checkpoint**: User Story 2 is independently functional and can be validated with quickstart scenarios 4 and 5.

---

## Phase 5: User Story 3 - Quick-Fill Start and End from the Trip Leg (Priority: P2)

**Goal**: Travelers can copy start and end values, including their timezone selections, from the selected trip leg into the event form without silently overwriting manual input.

**Independent Test**: Create or edit an event on a leg, invoke copy for start and end, confirm the values match the leg, save, and confirm the copied values persist.

### Implementation for User Story 3

- [X] T024 [US3] Add copy-from-leg controls beside the start and end date fields in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T025 [US3] Implement selected trip leg lookup and start/end copy methods in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T026 [US3] Copy the leg's start local date/time and start timezone into the event start fields in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T027 [US3] Copy the leg's end local date/time and end timezone into the event end fields while leaving the event end unchanged if the leg has no end in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T028 [US3] Protect manually entered start/end values by requiring explicit overwrite confirmation before copy applies in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T029 [US3] Ensure changing the selected trip leg updates the copy source without automatically overwriting event start/end fields in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`

**Checkpoint**: User Story 3 is independently functional and can be validated with quickstart scenarios 2 and 3.

---

## Phase 6: Polish and Cross-Cutting Concerns

**Purpose**: Final validation and cleanup across all stories.

- [X] T030 [P] Review `specs/008-event-detail-shortcuts/contracts/api.md` against implemented request/response names and update only if implementation intentionally differs
- [X] T031 [P] Review `specs/008-event-detail-shortcuts/quickstart.md` against the completed modal workflow and update only if validation steps changed
- [X] T032 Run focused API tests with `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj` from `specs/008-event-detail-shortcuts/quickstart.md`
- [X] T033 Run focused database tests with `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj` from `specs/008-event-detail-shortcuts/quickstart.md`
- [X] T034 Run focused web component tests with `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj` from `specs/008-event-detail-shortcuts/quickstart.md`
- [ ] T035 Run the Aspire app locally with `dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj` and manually validate quickstart scenarios 1-5 in `specs/008-event-detail-shortcuts/quickstart.md`

---

## Dependencies and Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; establishes shared contract and schema shape.
- **Foundational (Phase 2)**: Depends on Phase 1; blocks all user story work.
- **User Story 1 (Phase 3)**: Depends on Phase 2; delivers MVP timezone capture.
- **User Story 2 (Phase 4)**: Depends on Phase 2 and can proceed alongside US1 after shared fields compile.
- **User Story 3 (Phase 5)**: Depends on Phase 2 and the event form field state from US1.
- **Polish (Phase 6)**: Depends on the user stories selected for delivery.

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational. No dependency on US2 or US3.
- **US2 (P1)**: Can start after Foundational. Integrates with the same modal as US1 but remains independently testable by editing and saving existing fields.
- **US3 (P2)**: Depends on the event form having start/end local fields and timezone dropdown state from US1.

### Within Each User Story

- Contract and storage tasks before repository/API tasks.
- Repository/API tasks before web form submission tasks.
- Form model state before rendering controls.
- Copy behavior after selected leg lookup and timezone fields exist.
- Quickstart validation after each story checkpoint.

---

## Parallel Opportunities

- T002, T003, and T004 can run in parallel after T001 is understood because they touch separate contract files.
- T005 and T006 can run in parallel because they touch different SQL files.
- T007, T008, and T009 can be prepared in parallel after contract records exist, then reconciled during compile.
- US1 and US2 both touch `TrackedItemForm.razor`; coordinate edits or sequence them to avoid conflicts.
- T030 and T031 can run in parallel during polish because they update separate documentation files.

## Parallel Example: User Story 1

```text
Task: "T012 [US1] Inject the existing timezone options provider into src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor"
Task: "T017 [US1] Preserve timeline rendering after timezone-aware tracked item changes in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor"
```

## Parallel Example: User Story 2

```text
Task: "T022 [US2] Keep edit modal cancel/close behavior from persisting pending changes in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor"
Task: "T023 [US2] Confirm owner-scoped event edit failures still surface through the existing alert path in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor"
```

## Parallel Example: User Story 3

```text
Task: "T026 [US3] Copy the leg's start local date/time and start timezone into src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor"
Task: "T027 [US3] Copy the leg's end local date/time and end timezone into src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Stop and validate quickstart scenario 1.
5. Run the focused API, database, and web component test commands listed in Phase 6.

### Incremental Delivery

1. Complete Setup and Foundational phases.
2. Deliver US1 for timezone capture and persistence.
3. Deliver US2 for edit modal field labeling, ordering, notes, and validation.
4. Deliver US3 for copy-from-leg shortcuts.
5. Run quickstart scenarios 1-5 and focused test commands.

### Parallel Team Strategy

1. One developer completes schema/contracts while another prepares API/web impact review.
2. After Phase 2, one developer owns US1 timezone capture while another owns US2 modal field labeling/validation.
3. US3 starts once the US1 form state exists, then final validation runs across all stories.

---

## Independent Test Criteria

- **US1**: Save an event with explicit start and end timezone selections, reopen it, and confirm the selections and converted times are retained.
- **US2**: Edit an existing event's timezone selections, Confirmation/Reservation Code, Notes, and timeline-affecting fields, then confirm all saved values persist and cancel leaves old values unchanged.
- **US3**: Invoke copy-from-leg for start and end on a selected leg, confirm copied date/time and timezone values match the leg, then confirm manual values are not silently overwritten.