# Tasks: Estimated Expenses

**Input**: Design documents from `specs/017-estimated-expenses/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api.md](./contracts/api.md), [quickstart.md](./quickstart.md)

**Tests**: No test-first tasks are included because the specification does not request TDD. Focused test and validation tasks at the end run the commands from [quickstart.md](./quickstart.md).

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently. All user-facing wording uses "estimated cost" (per item) and "estimated total" (rollups).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it touches different files or has no dependency on incomplete tasks.
- **[Story]**: Maps implementation tasks to the user story they deliver.
- Every task includes exact file paths.

---

## Phase 1: Setup (Shared Contract and Storage Shape)

**Purpose**: Establish the shared `estimated_cost` storage shape and the contract fields used across all stories.

- [X] T001 Add a nullable `estimated_cost NUMERIC(12,2)` column to `tracked_items` with a non-negative check constraint (`CHECK (estimated_cost IS NULL OR estimated_cost >= 0)`, drop-if-exists then add) in `src/TripPlanner.Database/Scripts/Schema/009_estimated_expenses.sql`
- [X] T002 [P] Append `decimal? EstimatedCost` to `CreateTrackedItemRequest` and `UpdateTrackedItemRequest` in `src/TripPlanner.Contracts/TripItems/TripItemContracts.cs`
- [X] T003 [P] Append `decimal? EstimatedCost` to `TrackedItemDto` and `decimal EstimatedCostTotal` (default `0`) to `TripDetail` in `src/TripPlanner.Contracts/Trips/TripContracts.cs`
- [X] T004 [P] Append `decimal? EstimatedCost` to `TimelineItem` and `decimal EstimatedCostTotal` (default `0`) to `TimelineLeg` in `src/TripPlanner.Contracts/Timeline/TimelineContracts.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wire the shared SQL, repository mapping, validation, endpoint, and web client plumbing so an estimated cost can be persisted and returned before any user-facing behavior is built.

**Critical**: No user story work should begin until this phase is complete.

- [X] T005 Persist and return `estimated_cost` in the insert, update, and `@SelectByTrip` statements in `src/TripPlanner.Database/Scripts/Commands/TrackedItems/UpsertAndDeleteTrackedItems.sql`
- [X] T006 Map `EstimatedCost` into the insert/update parameters and read it back on select in `src/TripPlanner.Database/TripItems/TripItemRepository.cs`
- [X] T007 No change needed: tracked items round-trip through the `@SelectByTrip` query (T005), not `Queries/Trips/GetTripDetail.sql`, which only returns the trip header
- [X] T008 No code change needed: Dapper maps `estimated_cost` into `TrackedItemDto.EstimatedCost` via the `@SelectByTrip` alias, so the event modal round-trips the value (`src/TripPlanner.Database/Trips/TripReadRepository.cs` returns items unchanged)
- [X] T009 Validate the optional estimated cost (reject negatives, enforce two-decimal precision and the `NUMERIC(12,2)` magnitude bound) with a friendly message in `src/TripPlanner.Api/Features/TripItems/TrackedItemValidator.cs`
- [X] T010 No change needed: the create/update endpoints pass the request (including `EstimatedCost`) straight to the repository in `src/TripPlanner.Api/Features/TripItems/TrackedItemEndpoints.cs`
- [X] T011 No change needed: the web client serializes the request records (including `EstimatedCost`, null when cleared) via `PostAsJsonAsync`/`PutAsJsonAsync` in `src/TripPlanner.Web/Features/Trips/TripApiClient.cs`

**Checkpoint**: Contracts, database schema/SQL, repository mapping, API validation, endpoint, and web client agree on the optional estimated cost shape.

---

## Phase 3: User Story 1 - Record an Estimated Cost for an Itinerary Item (Priority: P1) 🎯 MVP

**Goal**: A traveler can enter, edit, and clear an optional estimated cost on an event in the event detail modal, and see it retained on reopen.

**Independent Test**: Open an event, enter an estimated cost, save, reopen, and confirm the value is retained; edit and clear it and confirm the changes persist; entering a negative value is rejected.

### Implementation for User Story 1

- [X] T012 [US1] Add an optional `EstimatedCost` (`decimal?`) field with a non-negative `[Range]` annotation to the item edit model in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T013 [US1] Render an "Estimated cost" numeric input (`min="0"`, `step="0.01"`) as a secondary field near Notes with its validation message in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`
- [X] T014 [US1] Populate the edit state from `TrackedItemDto.EstimatedCost` and submit the value on create/update, sending `null` when the field is cleared, in `src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor`

**Checkpoint**: User Story 1 is independently functional and can be validated with quickstart scenario 1.

---

## Phase 4: User Story 2 - See a Total Estimated Cost for the Whole Trip (Priority: P2)

**Goal**: A traveler sees an overall estimated total on the trip details page, equal to the sum of all leg-assigned item estimated costs, presented as a secondary detail.

**Independent Test**: Add estimated costs across the trip (including a zero and a no-estimate item), open the trip details page, and confirm the estimated total equals the sum of entered estimates; with no estimates, confirm a clear "no estimates yet" indication.

### Implementation for User Story 2

- [X] T015 [US2] Compute `TripDetail.EstimatedCostTotal` as the sum of leg-assigned tracked item estimated costs (ignoring nulls, `0` when none) where the full detail is assembled in `src/TripPlanner.Api/Features/Trips/GetTripDetail/GetTripDetailEndpoint.cs` (the repository's `GetDetailAsync` returns empty items, so the total is computed after items are loaded)
- [X] T016 [US2] Display the overall "estimated total" in a secondary, non-dominant position on the trip details page in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`
- [X] T017 [US2] Show a clear "no estimates yet" indication when the trip estimated total is zero/none instead of a blank value in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`

**Checkpoint**: User Stories 1 and 2 both work independently; the trip estimated total can be validated with quickstart scenario 2.

---

## Phase 5: User Story 3 - See Estimated Cost Totals Per Day or Leg (Priority: P3)

**Goal**: A traveler sees an estimated total for each trip leg in the travel leg column of the timeline, and the per-leg totals sum to the trip estimated total.

**Independent Test**: Spread estimated costs across legs, open the timeline, confirm each leg column shows a subtotal equal to its items' estimated costs, and confirm the per-leg totals add up to the trip estimated total.

### Implementation for User Story 3

- [X] T018 [US3] Return each tracked item's `estimated_cost` in the timeline items result set in `src/TripPlanner.Database/Scripts/Queries/Timeline/GetTripTimeline.sql`
- [X] T019 [US3] Map item `EstimatedCost` and compute each `TimelineLeg.EstimatedCostTotal` from its items (ignoring nulls, `0` when none) in `src/TripPlanner.Database/Timeline/TimelineRepository.cs`
- [X] T020 [US3] Display the leg "estimated total" in the travel leg column as a secondary detail in `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`
- [X] T021 [US3] Show a clear "no estimates" indication for a leg whose estimated total is zero/none in `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`

**Checkpoint**: All user stories are independently functional; per-leg totals can be validated with quickstart scenario 3.

---

## Phase 6: Polish and Cross-Cutting Concerns

**Purpose**: Final validation and cleanup across all stories.

- [X] T022 [P] Review `specs/017-estimated-expenses/contracts/api.md` against implemented request/response names and update only if implementation intentionally differs
- [X] T023 [P] Review `specs/017-estimated-expenses/quickstart.md` against the completed workflow and update only if validation steps changed
- [X] T024 Run focused database tests with `dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj` (upsert round-trip, non-negative constraint, leg/trip aggregation) per `specs/017-estimated-expenses/quickstart.md`
- [X] T025 Run focused API tests with `dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj` (validation, DTO round-trip, `estimatedCostTotal` and sum invariant) per `specs/017-estimated-expenses/quickstart.md`
- [X] T026 Run focused web component tests with `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj` (modal field, leg-column total, trip-detail total, empty state) per `specs/017-estimated-expenses/quickstart.md`
- [ ] T027 Run the Aspire app locally with `dotnet watch --non-interactive --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj` and manually validate quickstart scenarios 1-3 in `specs/017-estimated-expenses/quickstart.md` (requires a live PostgreSQL/Aspire environment; not run in this pass)

---

## Dependencies and Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup. Blocks all user stories.
- **User Stories (Phases 3-5)**: Each depends on Foundational. Once Foundational is complete, US1, US2, and US3 can proceed independently and largely in parallel (they touch different files).
- **Polish (Phase 6)**: Depends on the user stories being implemented.

### Story Dependencies

- **US1 (P1)**: Depends only on Foundational. Delivers the MVP (recording an estimated cost).
- **US2 (P2)**: Depends only on Foundational (uses the trip detail projection). Independent of US1's form work.
- **US3 (P3)**: Depends only on Foundational (uses the timeline projection). Independent of US1 and US2.

### Within Each Story

- Contract/SQL/repository changes precede the Razor display changes that consume them.
- Tasks editing the same file (e.g., multiple `TrackedItemForm.razor` tasks) run sequentially.

---

## Parallel Execution Examples

### Phase 1 (Setup) — contract files are independent

```text
T002  → src/TripPlanner.Contracts/TripItems/TripItemContracts.cs
T003  → src/TripPlanner.Contracts/Trips/TripContracts.cs
T004  → src/TripPlanner.Contracts/Timeline/TimelineContracts.cs
```

(T001 schema script can also run alongside these.)

### After Foundational — stories in parallel (different files)

```text
US1:  T012 → T013 → T014   (TrackedItemForm.razor)
US2:  T015 (TripReadRepository.cs) → T016 → T017 (TripDetails.razor)
US3:  T018 (GetTripTimeline.sql) → T019 (TimelineRepository.cs) → T020 → T021 (TripTimeline.razor)
```

### Phase 6 doc reviews

```text
T022  → contracts/api.md
T023  → quickstart.md
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup) and Phase 2 (Foundational).
2. Complete Phase 3 (US1) — traveler can record, edit, and clear an estimated cost on an event.
3. **STOP and validate** with quickstart scenario 1. This is a shippable increment: estimates are captured and persisted even before any rollup is shown.

### Incremental Delivery

1. Add **US2** for the overall trip estimated total on the trip details page → validate scenario 2.
2. Add **US3** for per-leg estimated totals in the timeline leg column → validate scenario 3 and confirm the sum invariant (per-leg totals equal the trip total).
3. Finish with Phase 6 polish and focused test runs.
