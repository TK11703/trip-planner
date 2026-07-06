# Implementation Plan: Timezone Configurations

**Branch**: `main` | **Date**: 2026-07-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/006-timezone-configurations/spec.md`

## Summary

Add timezone configuration to the authenticated profile form and trip leg workflow. A user profile stores one valid default timezone; the first trip leg in a trip defaults both start and end timezone selections to that profile timezone, while second and later legs default both selections to the end timezone used by the most recently created leg in the same trip. Every trip leg must save an explicit start timezone and end timezone, and calendar rendering must display leg start/end times as scheduled wall clock times instead of shifting the displayed hours to the viewer's current timezone.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Azure/Entra authentication and token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3, vanilla FullCalendar JavaScript interop.

**Storage**: PostgreSQL. Add profile timezone plus trip leg start/end local date-time and timezone fields through ordered SQL schema scripts in `src/TripPlanner.Database/Scripts/Schema`; access the fields through Dapper repositories and SQL files in `src/TripPlanner.Database`.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. Existing test infrastructure includes authenticated API tests, database-backed tests, Blazor component tests, and E2E tests.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Timezone selection lists should load with the profile and leg forms without perceptible delay. Calendar projection should remain within normal timeline load latency for a single trip.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, no jQuery, concise Program.cs setup through extension methods, environment-driven configuration, and authenticated owner-scoped access. Calendar event start/end values for trip legs must be sent as local wall-clock values without timezone offsets so FullCalendar does not convert the visible hours. Chronological validation must compare the start local date/time in its start timezone with the end local date/time in its end timezone.

**Scale/Scope**: One profile timezone per authenticated user and two required timezone selections per trip leg: start timezone and end timezone. Initial scope includes profile form selection, profile API/storage updates, trip leg contract/API/storage updates, defaulting rules for first and subsequent legs, wall-clock calendar projection for trip leg starts and ends, validation, migration/backfill handling for existing rows, and focused tests. Tracked item timezone behavior is out of scope unless needed to keep existing timeline projection compatible.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Timezone settings directly support dated trip legs and calendar review. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire, and container-ready ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | Profile and trip leg changes extend existing Minimal API feature slices. |
| IV. PostgreSQL with Dapper | PASS | Persistence is planned through PostgreSQL schema scripts, SQL files, and Dapper repositories. |
| V. Container App Readiness | PASS | No local-only timezone configuration or deployment assumption is introduced. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/006-timezone-configurations/
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
│   ├── Features/
│   │   ├── TripItems/
│   │   └── UserProfiles/
│   └── Security/
├── TripPlanner.Contracts/
│   ├── Profile/
│   ├── Timeline/
│   ├── TripItems/
│   └── Trips/
├── TripPlanner.Database/
│   ├── Scripts/
│   │   ├── Commands/TripLegs/
│   │   ├── Commands/UserProfiles/
│   │   ├── Queries/Timeline/
│   │   ├── Queries/TripLegs/
│   │   ├── Queries/UserProfiles/
│   │   └── Schema/
│   ├── Timeline/
│   ├── TripItems/
│   └── UserProfiles/
└── TripPlanner.Web/
    ├── Components/
    │   ├── Pages/
    │   ├── Timeline/
    │   └── TripItems/
    ├── Features/
    │   ├── Profile/
    │   └── Trips/
    └── wwwroot/js/

tests/
├── TripPlanner.Api.Tests/
│   ├── TripItems/
│   └── UserProfiles/
├── TripPlanner.Database.Tests/
│   ├── Timeline/
│   ├── TripItems/
│   └── UserProfiles/
├── TripPlanner.Web.Tests/
│   ├── Profile/
│   ├── Timeline/
│   └── TripItems/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. Timezone contract changes live in `TripPlanner.Contracts`; authenticated profile and trip leg endpoint validation lives in existing API slices; Dapper persistence and SQL scripts live in `TripPlanner.Database`; profile, trip leg, and calendar UI changes live in `TripPlanner.Web`.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is to store canonical timezone identifiers, save explicit start and end timezones on every trip leg, preserve local wall-clock values for calendar display, and keep timezone validation centralized enough for API and UI flows to share the same allowed choices.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Data model adds timezone semantics to profile defaults, trip leg start/end timing, and calendar projection. |
| II. .NET Application Stack | PASS | Design uses the established .NET, Blazor, Minimal API, Aspire, and FullCalendar surfaces. |
| III. Minimal API Vertical Slices | PASS | Profile and trip leg behavior remains isolated in vertical API slices. |
| IV. PostgreSQL with Dapper | PASS | Storage design uses SQL schema scripts and Dapper repositories without Entity Framework. |
| V. Container App Readiness | PASS | Timezone identifiers are persisted as data and do not depend on local machine timezone configuration. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

No constitutional violations require justification.
