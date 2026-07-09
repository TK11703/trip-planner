# Implementation Plan: Timeline Date Navigation

**Branch**: `014-timeline-date-navigation` | **Date**: 2026-07-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-timeline-date-navigation/spec.md`

## Summary

Make long trip timelines easier to navigate by adding a "Jump to date" control that lists every date within the trip's range. Selecting a date scrolls the horizontal timeline so that day aligns to the left edge (start) of the scroll area. The timeline card header's "Add leg" and "Add event" buttons are replaced by the "Jump to date" control; "Add event" already exists on each leg row, and "Add leg" moves to a footer at the bottom of the sticky trip-legs column. This is a front-end-only change to the existing Blazor `TripTimeline` component and its `TripDetails` host; no API or database changes are required.

## Technical Context

**Language/Version**: C# on .NET 10 (Blazor Web App, Interactive Server render mode)

**Primary Dependencies**: Blazor (`TripPlanner.Web`), existing `TripTimeline.razor` component, `tripTimeline.js` interop module, Bootstrap 5 utility/button/dropdown classes already used in the app

**Storage**: N/A — no persisted state; navigation is transient client-side view state only

**Testing**: bUnit component tests (`TripPlanner.Web.Tests`), Playwright end-to-end tests (`TripPlanner.E2E.Tests`)

**Target Platform**: Modern evergreen browsers rendering the Blazor Web front end; deployable as a container to Azure Container Apps

**Project Type**: Web application (Blazor front end + Minimal API + PostgreSQL); this feature touches only the front end

**Performance Goals**: Selecting a date settles the timeline on that day in under 3 seconds (SC-002); scroll positioning uses a direct offset computation, not per-frame work

**Constraints**: Reuse the existing horizontal scroll grid, slot width, and day/hour layout without changing timeline structure, zoom, or time granularity; preserve existing leg/event selection, editing, and sticky-header behaviors; keep controls keyboard-operable with accessible labels; honor current light/dark theme and branding

**Scale/Scope**: A single trip timeline that can span from one day to several weeks; the date list scales with trip length and must remain usable for long trips

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Trip Planning Domain | PASS | Improves navigation of trip itineraries/legs/events within a trip. |
| II. .NET Application Stack | PASS | Blazor front end on .NET 10; no stack changes; Aspire orchestration untouched. |
| III. Minimal API Vertical Slices | PASS | No API changes; timeline data endpoint already exists and is reused as-is. |
| IV. PostgreSQL with Dapper | PASS | No data-access changes; feature is presentation-only. |
| V. Container App Readiness | PASS | No new infrastructure, configuration, or local-only assumptions introduced. |

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check**: PASS — Phase 1 design keeps the change within the existing Blazor component, adds one JS interop function, and introduces no new projects, dependencies, or persistence. No new violations.

## Project Structure

### Documentation (this feature)

```text
specs/014-timeline-date-navigation/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── timeline-component-api.md   # TripTimeline public members used for navigation
│   └── timeline-js-interop.md      # tripTimeline JS module contract
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Web/
│   ├── Components/
│   │   ├── Timeline/
│   │   │   └── TripTimeline.razor          # Add jump control + legs-column footer; expose date list + ScrollToDate
│   │   └── Pages/Trips/
│   │       └── TripDetails.razor           # Replace header Add leg/Add event with Jump to date control
│   └── wwwroot/
│       ├── js/
│       │   └── tripTimeline.js             # Add scrollToDate(scrollEl, offsetX) interop
│       └── css/
│           └── app.css                     # Styles for jump-to-date control + legs-column footer

tests/
├── TripPlanner.Web.Tests/                  # bUnit tests for control rendering, date list, boundary states
└── TripPlanner.E2E.Tests/                  # Playwright test: jump to a date scrolls timeline to that day
```

**Structure Decision**: Web application. The change is confined to the Blazor front end (`TripPlanner.Web`). The `TripTimeline` component owns the scroll grid, so it exposes the trip's date list and a `ScrollToDateAsync` method; `TripDetails` renders the "Jump to date" control in the card header and delegates to the component via its existing `@ref`. Scrolling is performed by a new `tripTimeline.scrollToDate` JS interop that sets the scroll container's horizontal offset. No `TripPlanner.Api`, `TripPlanner.Database`, or `TripPlanner.Contracts` changes.

## Complexity Tracking

> No constitution violations. Section intentionally left empty.
