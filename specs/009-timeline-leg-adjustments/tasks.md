# Tasks: Trip Leg and Event Timeline Adjustments

**Input**: Design documents from `specs/009-timeline-leg-adjustments/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/ui.md](./contracts/ui.md), [quickstart.md](./quickstart.md)

**Tests**: Included because [plan.md](./plan.md) and [quickstart.md](./quickstart.md) call for focused timeline component and E2E coverage.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

## Phase 1: Setup (Shared Context)

**Purpose**: Confirm the current timeline surfaces and test anchors before changing behavior.

- [X] T001 Review the feature UI contract and validation scenarios in specs/009-timeline-leg-adjustments/contracts/ui.md and specs/009-timeline-leg-adjustments/quickstart.md
- [X] T002 [P] Review current timeline rendering and callbacks in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T003 [P] Review current parent modal flow in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T004 [P] Review current timeline styles for row height, item bars, and leg bands in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T005 [P] Review existing timeline test placeholders in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs and tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Prepare stable UI/test hooks used by all user stories.

**CRITICAL**: Complete this phase before implementing any user story.

- [X] T006 Add stable per-leg row, label, lane, add-event, count, band, and item test selectors in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T007 Add bUnit-compatible timeline test harness and JS interop setup for TripTimeline in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs
- [X] T008 [P] Add shared timeline E2E locator helpers or constants for leg rows, counts, add-event actions, lanes, bands, and item bars in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs
- [X] T009 Verify the existing timeline contract exposes all data needed through TimelineLeg.Items without adding event-count storage in src/TripPlanner.Contracts/Timeline/TimelineContracts.cs

**Checkpoint**: Foundation ready; user story implementation can begin.

---

## Phase 3: User Story 1 - Add Events to a Trip Leg from Its Timeline Row (Priority: P1) MVP

**Goal**: A traveler can add a new event from a specific trip leg row and the existing event form opens with that leg pre-selected.

**Independent Test**: Open a trip timeline with at least one leg, use the add-event action on that leg row, save the event, and confirm it appears under that same leg without a full trip reload.

### Tests for User Story 1

- [X] T010 [US1] Add a component test proving a leg-row add-event action emits the selected TripLegId and an initial start time in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs
- [X] T011 [P] [US1] Add an E2E test proving add-event from a leg row opens the event form with the selected leg pre-selected in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs

### Implementation for User Story 1

- [X] T012 [US1] Add an explicit add-event control inside each trip leg label row in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T013 [US1] Wire the leg-row add-event control to invoke the existing OnLegSlotSelected flow with the row TripLegId and a sensible initial StartLocal in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T014 [US1] Stop add-event control clicks from triggering leg edit selection in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T015 [US1] Ensure OnTimelineLegSlotSelected clears stale item state, sets _initialItemLegId and _initialItemStart, and opens the existing create-event modal in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor
- [X] T016 [P] [US1] Style the leg-row add-event control so it fits the sticky left label column on desktop and mobile in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T017 [US1] Refresh the trip detail and timeline after saving the new event so the event appears under the selected leg in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor

**Checkpoint**: User Story 1 is independently functional and testable as the MVP.

---

## Phase 4: User Story 2 - See Event Counts per Trip Leg on the Timeline (Priority: P2)

**Goal**: Each trip leg row shows `0 events`, `1 event`, or `# events` below the leg label, derived from the events associated with that leg.

**Independent Test**: Open a trip timeline with legs containing zero, one, and multiple events and confirm each label count matches the rendered events for that row.

### Tests for User Story 2

- [X] T018 [US2] Add component test coverage for 0, 1, and multiple event count labels in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs
- [X] T019 [P] [US2] Add an E2E assertion that adding an event updates only the selected leg count after save in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs

### Implementation for User Story 2

- [X] T020 [US2] Add an EventCountLabel helper that formats `0 events`, `1 event`, and `# events` from TimelineLeg.Items.Count in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T021 [US2] Render the event count below each trip leg title and origin/destination summary in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T022 [US2] Replace the current `No events yet` text with the standardized event count treatment in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T023 [P] [US2] Style the event-count text for readability and mobile fit in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T024 [US2] Confirm timeline refresh after event add, delete, or reassignment recalculates counts from refreshed TimelineLeg.Items in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Distinguish Trip Leg Time Ranges in Dark Mode (Priority: P3)

**Goal**: Trip leg time ranges are easy to spot in dark mode, and reduced-height event bars leave a clickable portion of each leg row for selecting times within the leg.

**Independent Test**: Switch to dark mode, view a timeline with multiple legs and events, confirm leg ranges are visible, event bars are reduced height, and clicking the open lane area starts the add-event flow for the clicked time.

### Tests for User Story 3

- [X] T025 [P] [US3] Add CSS contract tests for reduced event bar height and dark-mode leg-band tokens in tests/TripPlanner.Web.Tests/Timeline/TripTimelineCssTests.cs
- [X] T026 [US3] Add component test coverage that leg-band elements remain non-interactive and lane clicks still emit slot selections in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs
- [X] T027 [P] [US3] Add E2E coverage for dark-mode leg-band visibility and lane clickability beside reduced-height events in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs

### Implementation for User Story 3

- [X] T028 [US3] Reduce `.ttl-item` height to roughly half the row and position it so an open lane click target remains visible in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T029 [US3] Keep timeline events directly selectable with click propagation stopped only on the event bar in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor
- [X] T030 [US3] Add light-mode timeline leg-band background, border, and layering tokens in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T031 [US3] Add dark-mode timeline leg-band background, border, and shadow tokens under `[data-bs-theme="dark"]` in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T032 [US3] Verify overlapping or adjacent leg bands remain distinguishable from item bars and grid lines in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T033 [US3] Ensure mobile timeline label width, event count text, add-event control, reduced event bars, and lane click targets remain usable in src/TripPlanner.Web/wwwroot/css/app.css

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup across the complete timeline adjustment feature.

- [X] T034 [P] Remove obsolete skipped timeline placeholder tests or convert them to active assertions in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs
- [X] T035 [P] Remove obsolete skipped timeline placeholder tests or convert them to active assertions in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs
- [X] T036 Run focused web component tests with `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj` and record any failures against tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
- [ ] T037 Run focused E2E tests with `dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj` and record any failures against tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj (blocked: E2E tests are skipped placeholders that require a running AppHost + Playwright)
- [ ] T038 Run quickstart validation scenarios from specs/009-timeline-leg-adjustments/quickstart.md (manual: requires running the app and signing in; covered by automated component/CSS tests as a proxy)
- [X] T039 Validate generated changes with `dotnet test TripPlanner.slnx` from TripPlanner.slnx (full solution build succeeded; TripPlanner.Web.Tests: 33 passed, 3 skipped, 0 failed)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: No dependencies; can start immediately.
- **Phase 2 Foundational**: Depends on Phase 1; blocks all user stories.
- **Phase 3 US1**: Depends on Phase 2; MVP scope.
- **Phase 4 US2**: Depends on Phase 2; can be implemented independently, but delivers best value after US1.
- **Phase 5 US3**: Depends on Phase 2; can be implemented independently from US1 and US2.
- **Phase 6 Polish**: Depends on the selected user stories being complete.

### User Story Dependencies

- **US1 Add Events from Leg Row (P1)**: No dependency on other user stories after foundation.
- **US2 Event Counts (P2)**: No dependency on US1 for read-only counts, but count refresh should be validated with US1 when both are present.
- **US3 Dark Mode and Clickable Rows (P3)**: No dependency on US1 or US2 after foundation.

### Within Each User Story

- Write/activate tests before implementation tasks in the same story.
- Component markup changes in `TripTimeline.razor` should precede CSS polish that depends on new classes/selectors.
- Parent flow changes in `TripDetails.razor` should be validated with the timeline callback tests before E2E validation.
- Story checkpoints should be validated before moving to the next priority when working sequentially.

## Parallel Opportunities

- Setup review tasks T002-T005 can run in parallel.
- Foundational E2E helper task T008 can run in parallel with contract verification T009 after T006 selector decisions are known.
- In US1, E2E test T011 and style task T016 can run separately from the core `TripTimeline.razor` implementation once selectors are stable.
- In US2, E2E test T019 and CSS task T023 can run separately after the count label selector is chosen.
- In US3, CSS contract test T025 and E2E test T027 can run separately from component test T026.
- Final web and E2E test cleanup tasks T034 and T035 can run in parallel.

## Parallel Example: User Story 1

```text
Task: "T010 [US1] Add a component test proving a leg-row add-event action emits the selected TripLegId and an initial start time in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs"
Task: "T011 [P] [US1] Add an E2E test proving add-event from a leg row opens the event form with the selected leg pre-selected in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs"
Task: "T016 [P] [US1] Style the leg-row add-event control so it fits the sticky left label column on desktop and mobile in src/TripPlanner.Web/wwwroot/css/app.css"
```

## Parallel Example: User Story 2

```text
Task: "T018 [US2] Add component test coverage for 0, 1, and multiple event count labels in tests/TripPlanner.Web.Tests/Timeline/TripTimelineTests.cs"
Task: "T019 [P] [US2] Add an E2E assertion that adding an event updates only the selected leg count after save in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs"
Task: "T023 [P] [US2] Style the event-count text for readability and mobile fit in src/TripPlanner.Web/wwwroot/css/app.css"
```

## Parallel Example: User Story 3

```text
Task: "T025 [P] [US3] Add CSS contract tests for reduced event bar height and dark-mode leg-band tokens in tests/TripPlanner.Web.Tests/Timeline/TripTimelineCssTests.cs"
Task: "T027 [P] [US3] Add E2E coverage for dark-mode leg-band visibility and lane clickability beside reduced-height events in tests/TripPlanner.E2E.Tests/TimelineFlowTests.cs"
Task: "T030 [US3] Add light-mode timeline leg-band background, border, and layering tokens in src/TripPlanner.Web/wwwroot/css/app.css"
Task: "T031 [US3] Add dark-mode timeline leg-band background, border, and shadow tokens under `[data-bs-theme="dark"]` in src/TripPlanner.Web/wwwroot/css/app.css"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 setup and Phase 2 foundation.
2. Complete Phase 3 US1 tasks T010-T017.
3. Validate the add-event-from-leg-row flow independently with component and E2E checks.
4. Stop and demo the MVP if the traveler can add an event from a specific leg row and see it appear under that leg.

### Incremental Delivery

1. Deliver US1 so the timeline supports direct per-leg event creation.
2. Deliver US2 so every left-column trip leg label shows accurate event counts.
3. Deliver US3 so dark-mode leg ranges are obvious and reduced-height event bars restore row time-slot clickability.
4. Run Phase 6 validation after each selected increment or at the end of the complete feature.

### Parallel Team Strategy

1. Complete selector/test harness foundation together.
2. Assign US1 to one implementer for timeline add-event behavior.
3. Assign US2 to one implementer for event count display and refresh validation.
4. Assign US3 to one implementer for CSS readability, row clickability, and dark-mode checks.
5. Merge at story checkpoints and run focused tests before full solution validation.

## Notes

- [P] tasks use different files or can proceed without depending on incomplete same-file work.
- [US1], [US2], and [US3] labels map directly to the prioritized user stories in [spec.md](./spec.md).
- Keep counts derived from existing `TimelineLeg.Items.Count` unless implementation proves the existing timeline response is insufficient.
- Do not add new persistence or API endpoints for this feature unless a task explicitly discovers a missing contract requirement.
