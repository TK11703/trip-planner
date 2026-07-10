---
description: "Task list for Printable Trip View"
---

# Tasks: Printable Trip View

**Input**: Design documents from `/specs/020-printable-trip/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)

**Tests**: Included — the plan's Testing section explicitly calls for bUnit page tests, a pure-helper unit test, and a Playwright E2E flow.

**Organization**: Tasks are grouped by user story. This is a web-only vertical slice inside `TripPlanner.Web` that reuses the existing `GET /api/trips/{tripId}` → `TripDetail`; there are **no** API, contract, or database changes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 / US2 / US3 (from spec.md)
- Exact file paths are included in each task

## Path Conventions

- Web front end: `src/TripPlanner.Web/`
- Tests: `tests/TripPlanner.Web.Tests/`, `tests/TripPlanner.E2E.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Front-end scaffolding needed before the print page can render or trigger printing. No new NuGet or front-end libraries.

- [X] T001 [P] Add print CSS scaffolding — `.tp-print-*` document/table classes and a base `@media print` block (hides `.tp-print-actions` and any residual chrome) in [src/TripPlanner.Web/wwwroot/css/app.css](../../src/TripPlanner.Web/wwwroot/css/app.css)
- [X] T002 [P] Create the `window.print()` interop module `tripPrint.print()` in `src/TripPlanner.Web/wwwroot/js/tripPrint.js` and reference it from [src/TripPlanner.Web/Components/App.razor](../../src/TripPlanner.Web/Components/App.razor)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The chrome-free layout and the pure formatting/ordering helper that every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Create chrome-free `PrintLayout.razor` (inherits `LayoutComponentBase`, renders only `@Body` — no `NavMenu`, footer, or theme selector) in `src/TripPlanner.Web/Components/Layout/PrintLayout.razor`
- [X] T004 Create pure helper `TripPrintFormatting.cs` with `FormatDateTimeWithZone(DateTime local, string timeZoneId)` (`MM/dd/yyyy HH:mm TZ`, 24-hour, `InvariantCulture`, never throws), `OrderLegsChronologically`, `OrderEventsWithinLeg`, and `GroupEventsByLeg` in `src/TripPlanner.Web/Features/Trips/TripPrintFormatting.cs` (per [contracts/datetime-tz-format.md](contracts/datetime-tz-format.md))
- [X] T005 [P] Unit tests for `TripPrintFormatting` — datetime+TZ examples/edge cases (no end, blank/unknown zone, multi-TZ), leg ordering, event grouping/ordering, missing-optional handling — in `tests/TripPlanner.Web.Tests/Trips/TripPrintFormattingTests.cs`

**Checkpoint**: Chrome-free layout + formatting helper ready — user stories can begin.

---

## Phase 3: User Story 1 - Print a Complete Trip (Priority: P1) 🎯 MVP

**Goal**: A traveler opens a chrome-free printable page for a trip that shows all its details and can print it or save it as a PDF.

**Independent Test**: Open a trip with several legs and events, invoke its printable version, confirm the page shows the trip's details with no app chrome and prints/saves as a readable document; a trip with no legs/events still produces a sensible page.

### Tests for User Story 1

- [X] T006 [P] [US1] bUnit `TripPrintPageTests` — asserts chrome-free render (no `NavMenu`/footer), metadata + legs + events present, a **Print** control exists, empty-trip state message, and denied/not-found state when `GetDetailAsync` returns null (use `StubTripApiClient`) — in `tests/TripPlanner.Web.Tests/Trips/TripPrintPageTests.cs`

### Implementation for User Story 1

- [X] T007 [US1] Create `TripPrint.razor` at `@page "/trips/{TripId:guid}/print"` with `@attribute [Authorize]`, `@rendermode InteractiveServer`, and `@layout PrintLayout`; load `TripDetail` via `ITripApiClient.GetDetailAsync(TripId)`; handle loading / denied(null) / empty / populated states — in `src/TripPlanner.Web/Components/Pages/Trips/TripPrint.razor` (per [contracts/print-page-route.md](contracts/print-page-route.md))
- [X] T008 [US1] Create presentational `TripPrintDocument.razor` that renders a basic document (metadata block + itinerary table) from the loaded trip so US1 is independently viewable — in `src/TripPlanner.Web/Components/Trips/TripPrintDocument.razor`
- [X] T009 [US1] Add on-screen **Print** button (invokes `tripPrint.print()` via `IJSRuntime`) and **Back** link (to `/trips/{TripId}`) inside a `.tp-print-actions` region hidden under `@media print` — in `src/TripPlanner.Web/Components/Pages/Trips/TripPrint.razor`
- [X] T010 [US1] Add the empty-trip message (no legs/events) and reuse `TripAccessState` for the chrome-free denied/not-found state — in `src/TripPlanner.Web/Components/Pages/Trips/TripPrint.razor`

**Checkpoint**: A traveler can open `/trips/{tripId}/print`, see a chrome-free trip document, and print it. MVP complete.

---

## Phase 4: User Story 2 - See Every Trip Detail in the Printout (Priority: P2)

**Goal**: The printout is complete and correctly formatted — metadata first, legs as chronological row dividers, events as rows with one column per field, and every datetime combined with its timezone in a single column.

**Independent Test**: Open the printable version of a trip whose events mix times, locations, notes, and costs; confirm each detail appears in itinerary order and each Start/End cell reads `MM/dd/yyyy HH:mm TZ`.

### Tests for User Story 2

- [X] T011 [P] [US2] bUnit `TripPrintDocumentTests` — legs render as chronological full-width row dividers; event rows expose all eight columns; a Start/End cell matches `MM/dd/yyyy HH:mm {timeZoneId}`; missing optional fields render as empty cells (no error); events without a leg fall under an "Unassigned" divider — in `tests/TripPlanner.Web.Tests/Trips/TripPrintDocumentTests.cs`

### Implementation for User Story 2

- [X] T012 [US2] Add `PrintableTrip` / `PrintableLeg` / `PrintableEvent` view models and a mapper from `TripDetail` (using `TripPrintFormatting` for ordering, grouping, and datetime+TZ) — in `src/TripPlanner.Web/Features/Trips/TripPrintFormatting.cs` (per [data-model.md](data-model.md))
- [X] T013 [US2] Render the metadata block: `<h1>` name, date range `MM/dd/yyyy – MM/dd/yyyy`, description (omitted when empty), estimated cost total — in `src/TripPlanner.Web/Components/Trips/TripPrintDocument.razor`
- [X] T014 [US2] Render leg **row dividers** (`<th colspan>` spanning all columns) in chronological order with `Origin → Destination` and the leg's combined Start/End — in `src/TripPlanner.Web/Components/Trips/TripPrintDocument.razor`
- [X] T015 [US2] Render event rows with all eight columns (Type, Title, Location, Start, End, Confirmation, Est. Cost, Notes), combined datetime+TZ Start/End, empty cells for missing optionals, and the trailing "Unassigned" group — in `src/TripPlanner.Web/Components/Trips/TripPrintDocument.razor` (per [contracts/print-document-layout.md](contracts/print-document-layout.md))
- [X] T016 [US2] Add print/pagination CSS: repeat `thead` per page (`display: table-header-group`), `break-inside: avoid` on rows/legs, wrap long notes/titles (`overflow-wrap: anywhere`), and an ink-friendly light palette under `@media print` regardless of on-screen theme — in `src/TripPlanner.Web/wwwroot/css/app.css`

**Checkpoint**: The printout is complete, ordered, and correctly formatted, including across time zones and long trips.

---

## Phase 5: User Story 3 - Open the Printable Version Easily (Priority: P3)

**Goal**: A traveler reaches the printable version in one obvious step from the trip and can return without losing their place.

**Independent Test**: View a trip you own, invoke the clearly labeled Print action, confirm the print page opens for that trip; use Back to return to the trip.

### Tests for User Story 3

- [X] T017 [P] [US3] bUnit test — `TripDetails` shows an owner-only **Print** button linking to `/trips/{TripId}/print`, and it is absent when `IsOwner` is false — in `tests/TripPlanner.Web.Tests/Trips/TripDetailsPrintButtonTests.cs`

### Implementation for User Story 3

- [X] T018 [US3] Add an owner-only (`_trip.IsOwner`) **Print** button in the `TripDetails` header action group that navigates to `/trips/{TripId}/print` — in `src/TripPlanner.Web/Components/Pages/Trips/TripDetails.razor`
- [X] T019 [US3] Verify the print page **Back** link returns to `/trips/{TripId}` without losing place (confirms FR-011 alongside T009) — in `src/TripPlanner.Web/Components/Pages/Trips/TripPrint.razor`

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: End-to-end verification, accessibility, and validation across stories.

- [X] T020 [P] Playwright E2E — owner clicks **Print** on trip details → lands on the chrome-free print page containing the trip's legs and events (no nav/footer) — in `tests/TripPlanner.E2E.Tests/`
- [X] T021 [P] [US2] Accessibility pass — table `<caption>`, `<thead>` headers with `scope="col"`, divider cells as `scope="colgroup"` headers, single `<h1>` for `FocusOnNavigate` — in `src/TripPlanner.Web/Components/Trips/TripPrintDocument.razor`
- [ ] T022 Run [quickstart.md](quickstart.md) scenarios 1–7 manually against the running app
- [X] T023 [P] Run the full Web test suite and build to confirm green: `dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (T001, T002 in parallel).
- **Foundational (Phase 2)**: Depends on Setup — **blocks all user stories**. T003 and T004 can proceed together; T005 follows T004.
- **User Stories (Phase 3–5)**: All depend on Foundational.
  - US1 (P1) is the MVP and should come first.
  - US2 (P2) builds on the US1 document (extends `TripPrintDocument.razor` and the mapper).
  - US3 (P3) is independent of US2 and only needs the print route (T007) to exist.
- **Polish (Phase 6)**: Depends on the targeted user stories being complete.

### Story Dependencies

- **US1**: Needs Foundational (T003, T004). Self-contained page + basic document.
- **US2**: Needs US1's `TripPrintDocument.razor` (T008) and the helper (T004); refines structure/format.
- **US3**: Needs the print route (T007); otherwise independent of US2.

### Within Each Story

- Write the story's test first (T006, T011, T017) and let it fail before implementing.
- View models/mapping before rendering (T012 before T013–T015).
- Core render before CSS pagination polish.

### Parallel Opportunities

- Setup: T001 ‖ T002.
- Foundational: T003 ‖ T004 (then T005).
- Once T007/T008 exist, US2 rendering tasks and US3 entry-point tasks can proceed in parallel by different people.
- Polish: T020 ‖ T021 ‖ T023.

---

## Parallel Example: Foundational

```
# After Phase 1 completes, start these together:
T003  Create chrome-free PrintLayout.razor
T004  Create TripPrintFormatting.cs helper
# Then:
T005  Unit tests for TripPrintFormatting
```

---

## Implementation Strategy

- **MVP first**: Complete Phase 1 → Phase 2 → **Phase 3 (US1)**. This alone delivers a working, chrome-free, printable trip page — the core value travelers asked for.
- **Incremental delivery**: Add **US2** to guarantee completeness and the exact datetime+TZ / column formatting, then **US3** for the one-click entry point and easy return.
- **Validation**: Finish with Phase 6 (E2E, accessibility, quickstart, full test run).
