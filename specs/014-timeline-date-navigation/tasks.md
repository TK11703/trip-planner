# Tasks: Timeline Date Navigation

**Input**: Design documents from `/specs/014-timeline-date-navigation/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Included — the plan designates bUnit (`TripPlanner.Web.Tests`) and Playwright (`TripPlanner.E2E.Tests`) coverage for this feature.

**Organization**: Tasks are grouped by user story so each can be implemented and tested independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All paths are repository-relative.

## Path Conventions

Web application — front end only. Primary files:

- `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor`
- `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`
- `src/TripPlanner.Web/wwwroot/js/tripTimeline.js`
- `src/TripPlanner.Web/wwwroot/css/app.css`
- `tests/TripPlanner.Web.Tests/`
- `tests/TripPlanner.E2E.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm baseline before changes

- [X] T001 Confirm the Web project builds and its bUnit test project runs green as a baseline: `dotnet build src/TripPlanner.Web/TripPlanner.Web.csproj` and `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core scroll-positioning primitives that ALL navigation stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Add `scrollToDate(scrollEl, offsetX)` to the `tripTimeline` module in `src/TripPlanner.Web/wwwroot/js/tripTimeline.js` per [contracts/timeline-js-interop.md](contracts/timeline-js-interop.md): clamp `offsetX` to `[0, scrollWidth - clientWidth]`, use `scrollTo({ left, behavior: 'smooth' })` with a `prefers-reduced-motion` fallback to `'auto'`, and guard against a null element.
- [X] T003 In `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor` add the day-offset helper (`dayIndex = date.DayNumber - _start.DayNumber`, `offsetX = dayIndex * SlotsPerDay * SlotWidthPx`), private `_currentDate` state, and the core `public Task ScrollToDateAsync(DateOnly date)` that clamps to `[_start, _end]`, sets `_currentDate`, no-ops when the grid is not rendered, and invokes `tripTimeline.scrollToDate` (wrapped for `JSDisconnectedException`/`TaskCanceledException`).

**Checkpoint**: Foundation ready — user stories can now be built on top of `ScrollToDateAsync`

---

## Phase 3: User Story 1 - Jump Directly to a Chosen Date (Priority: P1) 🎯 MVP

**Goal**: A "Jump to date" control in the timeline card header lists every trip date; selecting one scrolls that day to the left edge and highlights it as the current position. The header's old Add-leg/Add-event buttons are replaced, with Add-leg relocated to a legs-column footer.

**Independent Test**: On a trip wide enough to require scrolling, open the Jump-to-date control, pick a mid-trip date, and confirm the timeline scrolls so that day sits at the left edge with the date highlighted.

### Tests for User Story 1

- [X] T004 [P] [US1] bUnit test in `tests/TripPlanner.Web.Tests/Timeline/TripTimelineJumpTests.cs`: the exposed `TripDates` contains exactly one entry per day from start to end inclusive, and the rendered legs-column "Add leg" footer appears only when `CanEdit` is true.
- [X] T005 [US1] Playwright test in `tests/TripPlanner.E2E.Tests/` (e.g. `TimelineDateNavigationTests.cs`): on a multi-day trip, selecting a mid-trip date leaves the `.ttl-scroll` container's `scrollLeft` at the expected day offset (`dayIndex * 1248`).

### Implementation for User Story 1

- [X] T006 [US1] In `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor` expose `public IReadOnlyList<DateOnly> TripDates` (start→end inclusive, empty until loaded), `public DateOnly? CurrentDate`, and `EventCallback OnAddLegRequested` per [contracts/timeline-component-api.md](contracts/timeline-component-api.md).
- [X] T007 [US1] In `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor` render an "Add leg" footer button at the bottom of the sticky trip-legs label column, shown only when `CanEdit`, invoking `OnAddLegRequested`.
- [X] T008 [US1] In `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor` remove the header "Add leg" and "Add event" buttons and add a "Jump to date" dropdown (top-right of the timeline card header) that lists `_timeline.TripDates`, calls `_timeline.ScrollToDateAsync(date)` on selection, and disables while the list is empty (loading).
- [X] T009 [US1] In `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor` wire the new `OnAddLegRequested` callback to the existing `OpenCreateLegModal` handler, and mark the currently selected date (`_timeline.CurrentDate`) as active in the Jump-to-date list to satisfy the current-position cue (FR-003).
- [X] T010 [P] [US1] In `src/TripPlanner.Web/wwwroot/css/app.css` add styles for the Jump-to-date control and the legs-column "Add leg" footer using existing theme CSS variables (light/dark aware).

**Checkpoint**: User Story 1 fully functional — a date jump works end to end, editing affordances preserved.

---

## Phase 4: User Story 2 - Step Between Days Quickly (Priority: P2)

**Goal**: Next-day / previous-day controls advance or retreat exactly one day and stop at the trip boundaries with clear feedback.

**Independent Test**: From any position, use Next/Previous and confirm one-day movement and boundary stops at the first and last days.

### Tests for User Story 2

- [X] T011 [P] [US2] bUnit test in `tests/TripPlanner.Web.Tests/Timeline/TripTimelineStepTests.cs`: `GoToNextDayAsync`/`GoToPreviousDayAsync` move `CurrentDate` by ±1 day, clamp at `_start`/`_end` (no movement past boundaries), and start from `_start`/`_end` when `CurrentDate` is null.

### Implementation for User Story 2

- [X] T012 [US2] In `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor` add `public Task GoToNextDayAsync()` and `public Task GoToPreviousDayAsync()` that clamp to `[_start, _end]`, no-op at boundaries, and reuse `ScrollToDateAsync`; expose whether each boundary is reached for the host to render feedback.
- [X] T013 [US2] In `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor` add Previous-day / Next-day controls near the Jump-to-date control, disabling/annotating them when the trip start/end boundary is reached (FR-006).
- [X] T014 [P] [US2] In `src/TripPlanner.Web/wwwroot/css/app.css` style the day-step controls and their disabled/boundary state.

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Orient to Today and Trip Boundaries (Priority: P3)

**Goal**: One-action shortcuts jump to trip start, trip end, and today; the Today shortcut is unavailable when today is outside the trip range.

**Independent Test**: Use Trip start / Trip end and confirm they land on the first/last days; on a trip containing today, use Today and confirm it lands on the current date; on a trip not containing today, confirm Today is unavailable.

### Tests for User Story 3

- [X] T015 [P] [US3] bUnit test in `tests/TripPlanner.Web.Tests/Timeline/TripTimelineShortcutTests.cs`: `TodayInRange` reflects whether today is within `[_start, _end]`; `GoToTripStartAsync`/`GoToTripEndAsync` set `CurrentDate` to `_start`/`_end`; `GoToTodayAsync` is a no-op when out of range.

### Implementation for User Story 3

- [X] T016 [US3] In `src/TripPlanner.Web/Components/Timeline/TripTimeline.razor` add `public bool TodayInRange`, `public Task GoToTripStartAsync()`, `public Task GoToTripEndAsync()`, and `public Task GoToTodayAsync()` (no-op when `!TodayInRange`) per the component contract.
- [X] T017 [US3] In `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor` add Trip-start / Trip-end / Today shortcut controls; hide or disable Today when `_timeline.TodayInRange` is false (FR-008).

**Checkpoint**: All three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Accessibility, edge cases, and end-to-end validation across stories

- [X] T018 Add accessible labels and keyboard operability to all navigation controls in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor` (aria-labels, focus order, Enter/Space activation, arrow-key menu navigation) to satisfy FR-012.
- [X] T019 [P] Playwright end-to-end test in `tests/TripPlanner.E2E.Tests/` covering day-step boundary clamping (Next stops at last day, Previous stops at first day) and the single-day-trip edge case.
- [ ] T020 Run the [quickstart.md](quickstart.md) manual validation scenarios (jump, step, shortcuts, layout relocation, light/dark, resize, single-day trip) and confirm success signals SC-001–SC-005. *(Pending: requires a running AppHost + manual browser session; logic is covered by the bUnit suite and skipped Playwright scenarios.)*

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories** (T002 → T003).
- **User Stories (Phase 3–5)**: All depend on Foundational completion. US1 (P1) is the MVP. US2 and US3 build on the same primitives and are independently testable.
- **Polish (Phase 6)**: Depends on the targeted user stories being complete.

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational. Delivers the core jump-to-date value and the header/footer layout change.
- **US2 (P2)**: Depends on Foundational; reuses `ScrollToDateAsync`. Independent of US1 UI but shares the header region (sequence T012 → T013 after US1's header edit to avoid conflicts in `TripDetails.razor`).
- **US3 (P3)**: Depends on Foundational; reuses `ScrollToDateAsync`. Shares the header region with US1/US2.

### Within Each User Story

- Tests are written first and should fail before implementation.
- Component (`TripTimeline.razor`) method additions precede host (`TripDetails.razor`) wiring.
- CSS can proceed in parallel with logic.

### File-Contention Notes (affects [P])

- `TripDetails.razor` is edited by T008/T009 (US1), T013 (US2), T017 (US3), T018 — these are **sequential**, not parallel.
- `TripTimeline.razor` is edited by T003, T006, T007, T012, T016 — **sequential**.
- `app.css` (T010, T014) and separate test files (T004, T011, T015, T019) are safe to parallelize where marked `[P]`.

### Parallel Opportunities

- Within US1: T004 (test) and T010 (CSS) can run in parallel with the component/page work once T006 lands.
- Across stories (if staffed): the bUnit test authoring tasks T004, T011, T015 target separate files and can be written in parallel after Foundational.

---

## Implementation Strategy

- **MVP scope**: Phases 1–3 (Setup + Foundational + User Story 1). This alone solves the reported problem — jumping directly to any date without scrolling — and completes the requested header/footer layout change.
- **Incremental delivery**: Add US2 (day stepping) then US3 (start/end/today shortcuts) as independent increments, each shippable and testable on its own.
- **Validation**: Finish with Phase 6 to harden accessibility and run the quickstart + end-to-end checks.
