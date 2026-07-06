# Tasks: Trip Leg Events and Timeline

**Input**: Design documents from `specs/007-trip-leg-events/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md), [quickstart.md](./quickstart.md)

**Tests**: The feature specification defines independent acceptance criteria, but does not request TDD. Tasks therefore include focused validation commands instead of pre-implementation test-writing tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish shared color and timeline building blocks used by the data, API, and Blazor timeline work.

- [X] T001 Add tracked item display color constants and validation helpers in src/TripPlanner.Contracts/TripItems/TripItemContracts.cs
- [X] T002 [P] Add custom trip timeline sizing, color, sticky header, and scroll CSS variables in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T003 [P] Add custom timeline browser interop scaffolding for scroll synchronization in src/TripPlanner.Web/wwwroot/js/tripTimeline.js
- [X] T004 [P] Review current trip detail modal entry points for timeline selection reuse in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Update shared contracts, schema, SQL, and repository surfaces before any story-specific behavior can safely build on leg-scoped events.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Add TripLegId and DisplayColor to tracked item request contracts in src/TripPlanner.Contracts/TripItems/TripItemContracts.cs
- [X] T006 [P] Add TripLegId and DisplayColor to tracked item response DTOs in src/TripPlanner.Contracts/Trips/TripContracts.cs
- [X] T007 [P] Replace flat timeline event contracts with TripTimeline, TimelineLeg, TimelineItem, and unassigned item contracts in src/TripPlanner.Contracts/Timeline/TimelineContracts.cs
- [X] T008 Add trip-leg event schema migration for tracked_items.trip_leg_id and tracked_items.display_color in src/TripPlanner.Database/Scripts/Schema/005_trip_leg_events.sql
- [X] T009 Update tracked item insert, update, delete, and select SQL for trip_leg_id and display_color in src/TripPlanner.Database/Scripts/Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql
- [X] T010 Update trip item repository method signatures and Dapper parameters for leg assignment and color in src/TripPlanner.Database/TripItems/TripItemRepository.cs
- [X] T011 Update timeline SQL projection to return leg rows, related items, unassigned items, color, and outside-leg flags in src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql
- [X] T012 Update timeline repository mapping for the new grouped timeline response in src/TripPlanner.Database/Timeline/TimelineRepository.cs
- [X] T013 Update trip API client models and serialization expectations for new tracked item and timeline contracts in src/TripPlanner.Web/Features/Trips/TripApiClient.cs
- [X] T014 Build the solution after shared contract and database changes using TripPlanner.slnx

**Checkpoint**: Foundation ready - user story implementation can now begin in priority order or in parallel by story.

---

## Phase 3: User Story 1 - Relate an Event to a Trip Leg (Priority: P1) MVP

**Goal**: A traveler can create or edit an event, choose the trip leg it belongs to, choose a display color, and see the saved event remain associated with that leg.

**Independent Test**: Open a trip with at least one leg, create an event, select a trip leg and color, save, leave and return to the trip, and confirm the event is saved under the selected leg. Attempt saving without a leg and confirm validation blocks the save.

### Implementation for User Story 1

- [X] T015 [US1] Validate required TripLegId, same-trip leg ownership, valid display color, and no-leg create guidance in src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs
- [X] T016 [US1] Update tracked item create and update endpoints to enforce leg assignment validation in src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs
- [X] T017 [US1] Persist TripLegId and DisplayColor during tracked item create and update operations in src/TripPlanner.Database/TripItems/TripItemRepository.cs
- [X] T018 [US1] Return TripLegId and DisplayColor from trip detail tracked item queries in src/TripPlanner.Database/Scripts/Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql
- [X] T019 [US1] Add trip leg selection and color selection fields to the tracked item form in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor
- [X] T020 [US1] Pass available trip legs into tracked item create and edit modal flows in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T021 [US1] Send TripLegId and DisplayColor through tracked item create and update calls in src/TripPlanner.Web/Features/Trips/TripApiClient.cs
- [X] T022 [US1] Validate event-to-leg behavior with focused API, database, and web test runs using tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj, tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj, and tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

**Checkpoint**: User Story 1 is independently functional and provides the MVP leg-scoped event workflow.

---

## Phase 4: User Story 2 - View a Trip Timeline Organized by Legs (Priority: P2)

**Goal**: A traveler can open a trip and view a custom resource timeline with trip legs listed on the left and a horizontally scrollable day/hour/30-minute grid containing each leg's events.

**Independent Test**: Open a trip with multiple legs and events across those legs, view the timeline, and confirm legs are ordered chronologically, empty legs remain visible, events render under the correct leg, day/date headers and hour headers align with 30-minute slots, and horizontal scrolling keeps context.

### Implementation for User Story 2

- [X] T023 [P] [US2] Update timeline endpoint response handling for grouped TripTimeline contracts in src/TripPlanner.Api/Features/Timeline/GetTimelineEndpoint.cs
- [X] T024 [US2] Build timeline repository grouping from SQL rows into ordered legs, ordered items, and unassigned items in src/TripPlanner.Database/Timeline/TimelineRepository.cs
- [X] T025 [US2] Calculate timeline start/end date range from trip dates, leg dates, and item dates in src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql
- [X] T026 [US2] Replace FullCalendar event projection with custom timeline view model loading in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T027 [US2] Render sticky leg resource labels, day/date header row, hour header row, and 30-minute slot grid in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T028 [US2] Render colored event blocks in the correct trip leg rows with stable same-time ordering in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T029 [US2] Implement synchronized horizontal scrolling and header alignment for the custom timeline in src/TripPlanner.Web/wwwroot/js/tripTimeline.js
- [X] T030 [US2] Remove FullCalendar CDN asset references from the trip app shell in src/TripPlanner.Web/Components/App.razor
- [X] T031 [US2] Style the custom resource timeline grid, empty-leg rows, color blocks, and overflow states in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T032 [US2] Update trip detail calendar copy and layout to describe the custom trip timeline in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T033 [US2] Validate custom timeline behavior with focused database and web test runs using tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj and tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

**Checkpoint**: User Stories 1 and 2 both work independently; the trip page shows leg-resource timeline rows with color-coded events.

---

## Phase 5: User Story 3 - Keep Events Related as Legs Change (Priority: P3)

**Goal**: A traveler can edit, reorder, or attempt to delete trip legs without losing event relationships, and the timeline exposes unassigned or out-of-leg-range items for review.

**Independent Test**: Change a leg's timing and confirm related events remain on that leg; reassign an event to another leg and confirm it moves; attempt deleting a leg with events and confirm deletion is blocked; confirm legacy unassigned items appear with a prompt to assign them.

### Implementation for User Story 3

- [X] T034 [US3] Add repository support to detect tracked items related to a trip leg before deletion in src/TripPlanner.Database/TripItems/TripItemRepository.cs
- [X] T035 [US3] Block trip leg deletion when related tracked items exist and return a validation error in src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs
- [X] T036 [US3] Preserve tracked item TripLegId relationships when trip leg timing is updated in src/TripPlanner.Database/Scripts/Commands/TripLegs/UpsertAndDeleteTripLegs.sql
- [X] T037 [US3] Include unassigned tracked items and outside-leg flags in timeline SQL results in src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql
- [X] T038 [US3] Display unassigned item prompts and outside-leg warning states in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T039 [US3] Open the existing tracked item modal from unassigned item prompts in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T040 [US3] Reopen existing leg and tracked item modals when timeline leg or item blocks are selected in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T041 [US3] Remove selected-item side pane state and markup from the trip detail page in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T042 [US3] Validate leg-change behavior with focused API, database, and web test runs using tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj, tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj, and tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj

**Checkpoint**: All user stories are independently functional and event relationships remain accountable as legs change.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation checks, and consistency cleanup across completed stories.

- [X] T043 [P] Review user-facing validation and empty-state copy for leg-required events in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor and src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T044 [P] Review API error response consistency for leg assignment and leg delete validation in src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs and src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs
- [X] T045 [P] Review custom timeline CSS for mobile and desktop overflow behavior in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T046 Run quickstart scenario validation from specs/007-trip-leg-events/quickstart.md
- [X] T047 Run full solution validation with TripPlanner.slnx

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational completion and is the MVP.
- **User Story 2 (Phase 4)**: Depends on Foundational completion and uses leg-scoped event data from US1, but can be implemented against seeded leg/item data if worked in parallel.
- **User Story 3 (Phase 5)**: Depends on Foundational completion and builds on the leg relationship and timeline projection from US1/US2.
- **Polish (Phase 6)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - no dependency on US2 or US3.
- **User Story 2 (P2)**: Can start after Foundational - depends on the data contract from US1 but can be independently tested with seeded related items.
- **User Story 3 (P3)**: Starts after US1 for relationship preservation and after US2 for timeline warning/unassigned item presentation.

### Parallel Opportunities

- T002, T003, and T004 can run in parallel because they touch CSS, JavaScript, and Razor review surfaces separately.
- T005, T006, and T007 can run in parallel because they update separate contract files.
- T011 and T012 can run after T007 and may proceed alongside T009/T010 once schema field names are agreed.
- T015 and T016 are API validation/endpoint tasks and can proceed in parallel with T019/T020 web form tasks after foundational contracts land.
- T023 can proceed in parallel with T024/T025 because endpoint shape and repository grouping touch separate files.
- T027, T028, T029, and T031 can be split across Razor, JavaScript, and CSS once T026 defines the component model.
- T034/T035 and T038/T039 can proceed in parallel after US1 and US2 data contracts are stable.
- T043, T044, and T045 can run in parallel during polish.

---

## Parallel Example: User Story 1

```text
Task: "T015 [US1] Validate required TripLegId, same-trip leg ownership, valid display color, and no-leg create guidance in src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs"
Task: "T019 [US1] Add trip leg selection and color selection fields to the tracked item form in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor"
Task: "T021 [US1] Send TripLegId and DisplayColor through tracked item create and update calls in src/TripPlanner.Web/Features/Trips/TripApiClient.cs"
```

## Parallel Example: User Story 2

```text
Task: "T023 [US2] Update timeline endpoint response handling for grouped TripTimeline contracts in src/TripPlanner.Api/Features/Timeline/GetTimelineEndpoint.cs"
Task: "T024 [US2] Build timeline repository grouping from SQL rows into ordered legs, ordered items, and unassigned items in src/TripPlanner.Database/Timeline/TimelineRepository.cs"
Task: "T031 [US2] Style the custom resource timeline grid, empty-leg rows, color blocks, and overflow states in src/TripPlanner.Web/wwwroot/css/app.css"
```

## Parallel Example: User Story 3

```text
Task: "T034 [US3] Add repository support to detect tracked items related to a trip leg before deletion in src/TripPlanner.Database/TripItems/TripItemRepository.cs"
Task: "T038 [US3] Display unassigned item prompts and outside-leg warning states in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor"
Task: "T044 Review API error response consistency for leg assignment and leg delete validation in src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs and src/TripPlanner.Api/Features/TripItems/TripLegEndpoints.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational.
3. Complete Phase 3: User Story 1.
4. Stop and validate event-to-leg creation, reassignment, and required-leg validation independently.
5. Demo leg-scoped event creation before replacing the whole trip timeline UI.

### Incremental Delivery

1. Complete Setup + Foundational to prepare shared contracts, schema, SQL, and repositories.
2. Add User Story 1 so users can save events related to trip legs with colors.
3. Add User Story 2 so the trip detail page shows the custom leg-resource timeline.
4. Add User Story 3 so leg edits/deletes and legacy unassigned items stay accountable.
5. Run quickstart and full solution validation.

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup and Foundational together.
2. Developer A implements US1 event-to-leg API, persistence, and form behavior.
3. Developer B implements US2 timeline contracts, repository projection, and endpoint handling.
4. Developer C implements US2/US3 timeline UI grid, modal selection, and warning states once contracts are stable.
5. Integrate by running story-focused validation at each checkpoint.

---

## Notes

- [P] tasks use different files and have no dependency on incomplete tasks in the same phase.
- [US1], [US2], and [US3] labels map tasks to the user stories in [spec.md](./spec.md).
- New tracked item create/update requests require `TripLegId` and `DisplayColor`.
- Existing legacy tracked items without `TripLegId` remain visible as unassigned until the traveler assigns them.
- The custom timeline uses trip legs as resources, day/date and hour headers, and fixed 30-minute slots.
- Selecting a timeline item opens the existing modal; the trip detail selected-item side pane should be removed.
- Avoid FullCalendar premium resource timeline and remove the non-premium FullCalendar dependency once the custom timeline renders the trip view.
