---
description: "Task list for Timeline Event Entry Experience Enhancements"
---

# Tasks: Timeline Event Entry Experience Enhancements

**Input**: Design documents from `/specs/018-timeline-event-entry-ux/`

**Prerequisites**: [plan.md](plan.md) (required), [spec.md](spec.md) (user stories), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Included. The plan's Testing section and [quickstart.md](quickstart.md) explicitly call for bUnit component tests and Playwright E2E tests, matching the existing `TripPlanner.Web.Tests/Timeline` and `TripPlanner.E2E.Tests` conventions.

**Organization**: Tasks are grouped by user story so each story can be implemented, tested, and delivered independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3
- Exact file paths are included in each task

## Path Conventions

Web application (Blazor front end). All changes are within `src/TripPlanner.Web/`; tests in `tests/TripPlanner.Web.Tests/` (bUnit) and `tests/TripPlanner.E2E.Tests/` (Playwright). No API, database, or contract changes.

---

## Phase 1: Setup

**Purpose**: Establish a known-good baseline before changing shared front-end components.

- [X] T001 Build the solution and run the existing timeline suite to confirm a green baseline: `dotnet build TripPlanner.slnx` then `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter FullyQualifiedName~Timeline`. Note current pass count so regressions are detectable.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-story prerequisites that must complete before any story.

No blocking foundational tasks are required. This is an additive, front-end-only feature on existing infrastructure (`TimelineItem.ItemType` and `Location` already exist; no schema, contract, or API changes). Each user story below is independently implementable and testable. Shared assets are created within the story that first needs them (the `TrackedItemIcon` component in User Story 2).

**Checkpoint**: Baseline green — user story implementation can begin.

---

## Phase 3: User Story 1 - Prefill Event Dates from the Active Timeline Date (Priority: P1) 🎯 MVP

**Goal**: Track the centered timeline date as the user scrolls or jumps, default a new event's start (and end = start + 1h) to that active date, and make the event form's end react to start changes until the end is edited directly.

**Independent Test**: Position the timeline on a mid-trip day (by scrolling and by using "Jump to date"), start a new event, and confirm the start defaults to that day and the end to one hour later; change the start in the form and confirm the end follows until manually edited; open an existing event and confirm its saved dates are untouched.

### Tests for User Story 1

- [X] T002 [P] [US1] bUnit test: invoking `SetCenteredDayIndex` on `TripTimeline` updates `CurrentDate` (clamped to trip range) and raises `OnActiveDateChanged`, and the `TripDetails` header "Jump to date" label reflects a scroll-driven active date, in tests/TripPlanner.Web.Tests/Timeline/TripTimelineActiveDateTests.cs
- [X] T003 [P] [US1] bUnit test: opening the Add-event form with the timeline's active date defaults the start to that date and the end to one hour after start (and falls back to trip start when no active date), in tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormDateDefaultTests.cs
- [X] T004 [P] [US1] bUnit test: changing the start in `TrackedItemForm` sets end = start + 1h, but stops auto-adjusting after the end is edited directly; editing an existing event does not overwrite its saved dates, in tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormReactiveEndTests.cs
- [X] T005 [P] [US1] Playwright E2E test: scrolling the timeline recenters and updates the active date, and clicking "+ Add event" defaults the new event to the active date, in tests/TripPlanner.E2E.Tests/TimelineActiveDateEntryFlowTests.cs

### Implementation for User Story 1

- [X] T006 [US1] Add a debounced (~120ms trailing) `scroll` listener and optional `dotNetRef` handling to `tripTimeline.init(scrollEl, dotNetRef)` that computes the centered day index (`floor((scrollLeft + (clientWidth - labelW)/2) / dayWidthPx)`, clamped to the `.ttl-day` count) and calls `dotNetRef.invokeMethodAsync('SetCenteredDayIndex', dayIndex)` only when the index changes; update `dispose` to remove the listener and clear state, in src/TripPlanner.Web/wwwroot/js/tripTimeline.js
- [X] T007 [US1] In `TripTimeline.razor`, add `[JSInvokable] void SetCenteredDayIndex(int)` (maps to `DateOnly`, clamps, updates `_currentDate`, `StateHasChanged`, raises new `OnActiveDateChanged` `EventCallback<DateOnly>`), create/dispose a `DotNetObjectReference<TripTimeline>`, and pass it into `tripTimeline.init` in `OnAfterRenderAsync`, in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor (depends on T006)
- [X] T008 [US1] In `TripTimeline.razor`, change `AddEventToLegAsync` so the per-leg "+ Add event" defaults the event start to the active date (`EffectiveCurrent`/`CurrentDate` combined with a sensible default time) instead of `leg.StartLocal`, while `OnLaneClickAsync` continues to pass the precise clicked slot start, in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor (depends on T007)
- [X] T009 [US1] In `TripDetails.razor`, subscribe to `TripTimeline.OnActiveDateChanged` to re-render the header "Jump to date" label/active highlight on scroll-driven changes, and ensure any header Add-event path seeds `_initialItemStart` from `_timeline?.CurrentDate` (fallback trip start), in src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor (depends on T007, T008)
- [X] T010 [P] [US1] In `TrackedItemForm.razor`, add an `_endManuallySet` flag and route `StartLocal`/`EndLocal` through change handlers so that on create the end auto-follows the start by one hour until the end is edited directly (and confirm `InitialStartsAt` still seeds start + end), leaving the edit path (`Item is not null`) unchanged, in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor
- [X] T011 [US1] Run the US1 tests and confirm green: `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter "FullyQualifiedName~ActiveDate|FullyQualifiedName~TrackedItemFormDate|FullyQualifiedName~TrackedItemFormReactiveEnd"` (depends on T006–T010)

**Checkpoint**: Active-date tracking and date-entry assistance are fully functional and independently testable (MVP).

---

## Phase 4: User Story 2 - Recognize Events on the Timeline by Type Icon (Priority: P2)

**Goal**: Each timeline entry shows an icon matching its type (event, reservation, activity, reminder), with a clear default icon for unknown/missing types, and the selected type's icon appears in the event form.

**Independent Test**: Place items of each type on the timeline and confirm each shows a type-appropriate icon, two items of the same type share an icon, changing an item's type updates its icon, and an unknown type shows the default icon.

### Tests for User Story 2

- [X] T012 [P] [US2] bUnit test: `TrackedItemIcon` renders a distinct glyph for each of `event`/`reservation`/`activity`/`reminder` (case-insensitive) and a default glyph for null/unknown types, in tests/TripPlanner.Web.Tests/TripItems/TrackedItemIconTests.cs
- [X] T013 [P] [US2] bUnit test: a timeline item button renders the `TrackedItemIcon` for its `ItemType`, same-type items render the same icon, and re-rendering with a changed type updates the icon, in tests/TripPlanner.Web.Tests/Timeline/TripTimelineIconTests.cs

### Implementation for User Story 2

- [X] T014 [US2] Create `TrackedItemIcon.razor` with `Type` (string?) and optional `CssClass` parameters, mapping each known `ItemType` to a distinct inline-SVG glyph using `currentColor`, with a default glyph for unknown/missing types and `aria-hidden="true"` on decorative SVGs, in src/TripPlanner.Web/Components/Shared/TrackedItemIcon.razor
- [X] T015 [P] [US2] Render `<TrackedItemIcon Type="item.ItemType" ... />` before the `ttl-item-title` span on each timeline item button in src/TripPlanner.Web/Components/Timeline/TripTimeline.razor (depends on T014)
- [X] T016 [P] [US2] Render `<TrackedItemIcon Type="_model.ItemType" ... />` next to the Type `InputSelect` in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor (depends on T014)
- [X] T017 [P] [US2] Add CSS to size/position the type icon within the timeline item button and the form type selector without materially changing item height, in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T018 [US2] Run the US2 tests and confirm green: `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter "FullyQualifiedName~TrackedItemIcon|FullyQualifiedName~TripTimelineIcon"` (depends on T014–T017)

**Checkpoint**: Timeline entries are visually distinguishable by type; independently testable.

---

## Phase 5: User Story 3 - Open an Event's Location on a Map (Priority: P3)

**Goal**: Present the location as an address, validate it as map-capable when entered, and offer a globe icon that opens the entered location in an external map — only when a valid location is present.

**Independent Test**: Enter a valid place/address and confirm the globe action opens a map for it in a new tab; enter an invalid value and confirm validation flags it and the globe is not offered; confirm an empty location is allowed with no map action; confirm returning from the map preserves unsaved event edits.

### Tests for User Story 3

- [X] T019 [P] [US3] bUnit test: location validation accepts empty and valid place/address values and rejects values that are solely whitespace/punctuation or over the length bound, in tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormLocationValidationTests.cs
- [X] T020 [P] [US3] bUnit test: the globe/map action is rendered only when the location is present and valid, uses the location text as entered in the map URL, and opens with reverse-tabnabbing-safe attributes (`target="_blank"` + `rel="noopener"`), in tests/TripPlanner.Web.Tests/TripItems/TrackedItemFormLocationMapTests.cs
- [X] T021 [P] [US3] Playwright E2E test: entering a valid location and activating the globe opens a maps URL for that location in a new context, in tests/TripPlanner.E2E.Tests/TimelineLocationMapFlowTests.cs

### Implementation for User Story 3

- [X] T022 [US3] In `TrackedItemForm.razor`, present the Location field as an address (label/placeholder) and add map-capable validation (custom validation attribute or `Validate` rule): empty is valid; non-empty must be trimmed, within a reasonable length (≤ 200), and contain at least one letter or digit; surface an inline `ValidationMessage`, in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor
- [X] T023 [US3] In `TrackedItemForm.razor`, add a globe icon button/link next to the Location field that opens `https://www.bing.com/maps?q={UrlEncode(Location)}` in a new tab with `rel="noopener noreferrer"`, enabled only when the location is present and valid, with `aria-label="Open location in a map"`, in src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor (depends on T022)
- [X] T024 [P] [US3] Add CSS for the location globe button styling and alignment with the input, in src/TripPlanner.Web/wwwroot/css/app.css
- [X] T025 [US3] Run the US3 tests and confirm green: `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj --filter "FullyQualifiedName~Location"` (depends on T022–T024)

**Checkpoint**: Locations are validated and actionable on a map; independently testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Accessibility, regression safety, and final validation across all stories.

- [X] T026 [P] Accessibility pass: confirm the location globe and type icons expose accessible labels (globe is a real button/link, decorative SVGs `aria-hidden`), are keyboard-operable, and reuse theme CSS variables for light/dark, across src/TripPlanner.Web/Components/Timeline/TripTimeline.razor, src/TripPlanner.Web/Components/TripItems/TrackedItemForm.razor, and src/TripPlanner.Web/Components/Shared/TrackedItemIcon.razor
- [X] T027 [P] Run the full build and complete test suite and confirm no regressions (especially existing `Timeline` tests): `dotnet build TripPlanner.slnx` then `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj` and `dotnet test tests/TripPlanner.E2E.Tests/TripPlanner.E2E.Tests.csproj`
- [X] T028 Validate the manual scenarios in specs/018-timeline-event-entry-ux/quickstart.md end to end (Scenarios 1–5) and confirm each Success Criterion (SC-001 … SC-006) is met

---

## Dependencies & Execution Order

- **Setup (Phase 1)** → **User Stories (Phases 3–5)** → **Polish (Phase 6)**. Phase 2 has no blocking tasks.
- **User Story 1 (P1)**: T006 → T007 → T008 → T009; T010 is independent (form file); tests T002–T005 authored first, verified at T011.
- **User Story 2 (P2)**: T014 → {T015, T016, T017 in parallel} → verify T018; tests T012–T013 authored first.
- **User Story 3 (P3)**: T022 → T023; T024 independent (CSS); verify T025; tests T019–T021 authored first.
- **Story independence**: US1, US2, and US3 are independently deliverable. They touch overlapping files (`TrackedItemForm.razor`, `TripTimeline.razor`, `app.css`) only across different phases, so complete one story's edits to a shared file before starting the next story's edits to it.

## Parallel Execution Examples

- **US1 tests together**: T002, T003, T004, T005 (distinct new test files).
- **US2 after `TrackedItemIcon` exists**: T015 (timeline), T016 (form), T017 (CSS) in parallel — different files, all depend only on T014.
- **US3 CSS alongside logic**: T024 (CSS) parallel to T022/T023 sequencing in the form.
- **Polish**: T026 and T027 in parallel.

## Implementation Strategy

- **MVP first**: Deliver User Story 1 (Phases 1 + 3) for immediate value — scroll/jump active-date tracking with new events defaulting their dates and a reactive end.
- **Incremental delivery**: Add User Story 2 (type icons) for timeline readability, then User Story 3 (mappable, validated location), each shippable on its own.
- **No backend risk**: All changes are presentation-only within `TripPlanner.Web`; no schema, contract, or API changes, so each story can ship without coordinated backend deployment.
