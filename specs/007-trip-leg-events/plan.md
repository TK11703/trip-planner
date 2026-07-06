# Implementation Plan: Trip Leg Events and Timeline

**Branch**: `main` | **Date**: 2026-07-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/007-trip-leg-events/spec.md`

## Summary

Replace the existing flat FullCalendar trip calendar with a first-party trip resource timeline that displays trip legs as the left-side resources and a horizontally scrollable calendar grid on the right. Tracked items must belong to a trip leg, carry a selectable display color, render in the correct leg row on the custom timeline, and open the existing leg/item modal when selected instead of populating a separate details pane.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests; vanilla JavaScript/CSS only where browser scrolling and grid measurement require it.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Azure/Entra authentication and token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3. FullCalendar currently exists but the premium resource timeline feature will not be used.

**Storage**: PostgreSQL. Add tracked item trip leg relationship and event display color through ordered SQL schema scripts in `src/TripPlanner.Database/Scripts/Schema`; access fields through Dapper repositories and SQL files in `src/TripPlanner.Database`.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. Existing test infrastructure includes authenticated API tests, database-backed tests, Blazor component tests, and E2E tests.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Timeline load should stay within normal trip detail latency for one trip. Horizontal scrolling should remain smooth for typical trip ranges of days to several weeks, with fixed row heights and deterministic column widths to avoid layout shift.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, no jQuery, concise Program.cs setup through extension methods, environment-driven configuration, and authenticated owner-scoped access. Do not use FullCalendar premium resource timeline. The timeline must use fixed calendar cells: day/date header row, hour header row, then one row per trip leg; each hour must be represented as two 30-minute slots. Selecting a timeline item opens the existing modal for that item rather than a side details pane.

**Scale/Scope**: One custom timeline on the trip detail page; trip legs are resources; tracked items are leg-scoped timeline events with selectable color. Scope includes schema/contracts/API/repository changes, validation for same-trip leg assignment, leg deletion protection when items exist, custom timeline projection, Blazor UI replacement, modal selection behavior, and focused tests. Drag/drop resizing and multi-leg events are out of scope for this feature.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Relating events to dated legs and presenting the trip as a leg-based timeline directly supports itinerary planning. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire, and container-ready ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | Trip item and timeline behavior extends existing Minimal API feature slices. |
| IV. PostgreSQL with Dapper | PASS | Persistence is planned through PostgreSQL schema scripts, SQL files, and Dapper repositories. |
| V. Container App Readiness | PASS | The custom timeline and data model introduce no local-only deployment assumptions. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/007-trip-leg-events/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── api.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Api/
│   └── Features/
│       ├── Timeline/
│       └── TripItems/
├── TripPlanner.Contracts/
│   ├── Timeline/
│   ├── TripItems/
│   └── Trips/
├── TripPlanner.Database/
│   ├── Scripts/
│   │   ├── Commands/TrackedItems/
│   │   ├── Commands/TripLegs/
│   │   ├── Queries/Timeline/
│   │   └── Schema/
│   ├── Timeline/
│   └── TripItems/
└── TripPlanner.Web/
    ├── Components/
    │   ├── Pages/Trips/
    │   ├── Timeline/
    │   └── TripItems/
    ├── Features/Trips/
    └── wwwroot/
        ├── css/
        └── js/

tests/
├── TripPlanner.Api.Tests/
│   ├── Timeline/
│   └── TripItems/
├── TripPlanner.Database.Tests/
│   ├── Timeline/
│   └── TripItems/
├── TripPlanner.Web.Tests/
│   ├── Timeline/
│   └── TripItems/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. Contract changes live in `TripPlanner.Contracts`; authenticated trip item and timeline endpoint validation lives in existing API slices; Dapper persistence and SQL scripts live in `TripPlanner.Database`; the custom resource timeline and modal behavior live in `TripPlanner.Web`.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is to remove the dependency on FullCalendar for trip timeline rendering, create a first-party Blazor timeline grid that treats trip legs as resources, and persist tracked item leg assignment plus display color as core event fields.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Data model and UI organize dated trip legs and events as the primary itinerary view. |
| II. .NET Application Stack | PASS | Design uses the established .NET, Blazor, Minimal API, Aspire, and PostgreSQL surfaces. |
| III. Minimal API Vertical Slices | PASS | Trip item and timeline behavior remains isolated in vertical API slices. |
| IV. PostgreSQL with Dapper | PASS | Storage design uses SQL schema scripts and Dapper repositories without Entity Framework. |
| V. Container App Readiness | PASS | The timeline is application UI and persisted trip data; no environment-specific deployment coupling is introduced. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

No constitutional violations require justification.
