# Implementation Plan: Event Detail Fields and Quick-Fill Shortcuts

**Branch**: `main` | **Date**: 2026-07-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/008-event-detail-shortcuts/spec.md`

## Summary

Adjust the existing event details modal and tracked item vertical slice so events store explicit start/end local times with timezone selections, keep a separate optional Confirmation/Reservation Code input before the Notes field, and provide copy buttons that populate the event start/end values from the selected trip leg. The implementation should extend existing tracked item contracts/storage/API/UI rather than introduce a new event concept.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Azure/Entra authentication and token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3, and the existing shared timezone options provider.

**Storage**: PostgreSQL. Extend `tracked_items` with event start/end local date-time and timezone fields through ordered SQL schema scripts in `src/TripPlanner.Database/Scripts/Schema`; continue using existing `confirmation_code` and `notes` columns, adding validation for the 255-character confirmation/reservation code and 2,000-character notes limit. Access fields through Dapper repositories and SQL files in `src/TripPlanner.Database`.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. Existing test infrastructure includes authenticated API tests, database-backed tests, Blazor component tests, and E2E tests.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Event create/edit should remain within normal trip detail modal response time. Timezone dropdowns should render without perceptible delay by reusing the existing timezone options provider. Copy-from-leg should be immediate client-side behavior once the trip leg list is loaded.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, no jQuery, concise Program.cs setup through extension methods, environment-driven configuration, and authenticated owner-scoped access. Event start/end timezone selection must reuse the same valid timezone choices as trip legs. Event start and end must be stored as unambiguous instants derived from local wall-clock values plus their selected timezones. Copy-from-leg must not silently overwrite manual event values.

**Scale/Scope**: One event details modal and the tracked item create/update/detail surfaces. Scope includes contracts, database schema/scripts, repository mapping, API validation, Blazor form layout/validation, copy-from-leg interactions, timeline projection compatibility, and focused tests. Attachments, cost fields, reusable event templates, and multi-event bulk editing are out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Event timing details, confirmation/reservation code, notes, and leg-based quick-fill directly support itinerary planning. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire, and container-ready ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | Tracked item behavior extends the existing Minimal API feature slice. |
| IV. PostgreSQL with Dapper | PASS | Persistence is planned through PostgreSQL schema scripts, SQL files, and Dapper repositories. |
| V. Container App Readiness | PASS | Timezone identifiers and event details are stored as application data with no local-only deployment assumptions. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/008-event-detail-shortcuts/
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
│       └── TripItems/
├── TripPlanner.Contracts/
│   ├── Timeline/
│   ├── TripItems/
│   └── Trips/
├── TripPlanner.Database/
│   ├── Scripts/
│   │   ├── Commands/TrackedItems/
│   │   ├── Queries/Timeline/
│   │   └── Schema/
│   ├── Timeline/
│   └── TripItems/
└── TripPlanner.Web/
  ├── Components/
  │   └── TripItems/
  └── Features/
    ├── Timezones/
    └── Trips/

tests/
├── TripPlanner.Api.Tests/
│   └── TripItems/
├── TripPlanner.Database.Tests/
│   └── TripItems/
├── TripPlanner.Web.Tests/
│   └── TripItems/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. Tracked item contract changes live in `TripPlanner.Contracts`; authenticated create/update validation lives in the existing TripItems API slice; schema, SQL, and instant conversion live in `TripPlanner.Database`; the modal layout, timezone dropdowns, and copy-from-leg behavior live in `TripPlanner.Web`.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is to store explicit event local start/end values plus timezone IDs, derive persisted instants server-side, reuse trip-leg timezone options and conversion patterns, preserve the existing confirmation/notes columns with stricter validation, and implement copy-from-leg as form-level behavior in the Blazor event modal.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Data model improves actionable trip events and leg-aligned scheduling. |
| II. .NET Application Stack | PASS | Design uses the established .NET, Blazor, Minimal API, Aspire, and PostgreSQL surfaces. |
| III. Minimal API Vertical Slices | PASS | Create/update/detail changes remain isolated in the tracked item vertical slice. |
| IV. PostgreSQL with Dapper | PASS | Storage design uses SQL schema scripts and Dapper repositories without Entity Framework. |
| V. Container App Readiness | PASS | Timezone IDs and detail fields are persisted data and do not depend on local machine settings. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
