# Implementation Plan: Modern UI Refresh

**Branch**: `main` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/004-modern-ui-refresh/spec.md`

## Summary

Refresh the existing Trip Planner user experience with a cohesive, modern, responsive Blazor Web App theme that preserves the Adventurous explorer brand direction across public and signed-in surfaces. The design adds coordinated light and dark theme modes, persists signed-in travelers' selected theme preference per account across sign-ins and devices, and defaults users with no saved preference plus unauthenticated visitors to the device/browser color setting. The refresh must preserve existing trip-planning behavior, authenticated ownership boundaries, Minimal API vertical slices, PostgreSQL/Dapper persistence, Bootstrap 5.3 usage, Aspire/container readiness, and the no MVC/no EF/no jQuery constraints from the baseline plan.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, database abstractions, shared contracts, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, existing Azure Entra/OIDC authentication, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3 theming utilities, existing vanilla JavaScript/browser interop only where needed for color-scheme detection or theme application. No MVC, Entity Framework, or jQuery.

**Storage**: PostgreSQL. Signed-in traveler theme preference is account-scoped persisted data accessed through Dapper. SQL schema/query files live in `src/TripPlanner.Database` rather than embedded SQL strings. Unauthenticated visitor/device defaults do not require account persistence.

**Testing**: .NET test projects using xUnit, bUnit for Blazor components/theme behavior, ASP.NET Core `WebApplicationFactory` for Minimal API preference-contract tests, Playwright for responsive/browser color-mode flows, and database-backed tests using Testcontainers or Aspire-managed PostgreSQL where persistence is required.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Theme application should avoid noticeable flash between initial page load and selected/default mode, keep primary surfaces responsive on desktop/tablet/phone, and preserve existing trip list/detail/calendar responsiveness for normal personal-use datasets.

**Constraints**: Preserve existing project constraints from `specs/001-modern-trip-planner/plan.md`: .NET 10, Blazor Web App with server-side interactivity, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Bootstrap 5.3, no MVC, no EF, no jQuery, Program.cs extension methods, environment-driven configuration, and container-ready components. The refresh must not add new required trip-planning data fields or weaken authentication, authorization, or ownership boundaries.

**Scale/Scope**: Primary surfaces in scope are landing, recent/all trips, trip details, FAQ, about, main navigation, modal-related flows, calendar experiences, loading/empty/validation/access-denied/unavailable states, and account-level theme preference. Out of scope: new trip-planning features, collaboration, native mobile apps, marketing campaign work, and full implementation task breakdown.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Refresh improves trip-planning surfaces while preserving itineraries, dated trip legs, events, reservations, activities, and calendar behavior. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire composition, and container-ready ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | Theme preference persistence is planned as a Minimal API vertical slice with colocated request/response contracts and handlers; Program.cs setup remains extension-based. |
| IV. PostgreSQL with Dapper | PASS | Account-level theme preference is persisted in PostgreSQL through Dapper with SQL files owned by the database project. |
| V. Container App Readiness | PASS | Configuration remains environment-driven and browser/device defaults require no local-only deployment assumption. |

**Pre-design gate**: PASS. No violations require justification and no Technical Context clarifications remain unresolved.

## Project Structure

### Documentation (this feature)

```text
specs/004-modern-ui-refresh/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api.md
│   ├── brand-system.md
│   └── ui-surfaces.md
└── tasks.md             # Generated later by /speckit.tasks, not by this plan
```

### Source Code (repository root)

```text
src/
├── TripPlanner.AppHost/                 # Aspire local orchestration
├── TripPlanner.ServiceDefaults/         # Shared Aspire service defaults
├── TripPlanner.Web/                     # Blazor Web App, OIDC, server-side interactivity
│   ├── Components/
│   │   ├── Layout/                      # Navigation, shell, theme entry points
│   │   ├── Pages/                       # Landing, FAQ, about, auth states
│   │   └── Trips/                       # Trip list/detail/calendar surfaces
│   ├── Features/
│   │   ├── Home/
│   │   ├── Trips/
│   │   ├── Theme/                       # Planned vertical UI slice for mode selection/application
│   │   └── Timeline/
│   ├── Extensions/                      # Program.cs middleware/auth/UI setup extensions
│   └── wwwroot/                         # Bootstrap 5.3 assets, app CSS, brand graphic/icon assets
├── TripPlanner.Api/                     # Authenticated Minimal API middle tier
│   ├── Features/
│   │   ├── Trips/
│   │   ├── TripItems/
│   │   ├── ThemePreferences/            # Planned account-scoped preference slice
│   │   └── Audit/
│   └── Extensions/                      # Program.cs auth/endpoints/openapi setup extensions
├── TripPlanner.Contracts/               # Shared DTOs and request/response contracts
└── TripPlanner.Database/                # Dapper abstractions and SQL files
    ├── Scripts/
    │   ├── Schema/
    │   └── Queries/
    └── Migrations/

tests/
├── TripPlanner.Api.Tests/
├── TripPlanner.Web.Tests/
├── TripPlanner.Database.Tests/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. UI refresh assets and components live in `TripPlanner.Web`; persisted account preference contracts and endpoints are planned as a small vertical slice in `TripPlanner.Api` with shared DTOs in `TripPlanner.Contracts`; Dapper mappings and SQL files remain in `TripPlanner.Database`.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The supplied decisions and existing project constraints resolve all Technical Context choices; no clarification items remain.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [contracts/brand-system.md](./contracts/brand-system.md)
- [contracts/ui-surfaces.md](./contracts/ui-surfaces.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Data model and contracts preserve trip, itinerary, calendar, FAQ/about, and access-state behavior while adding only visual-system and theme-preference concepts. |
| II. .NET Application Stack | PASS | Design remains .NET 10, Blazor Web App with server-side interactivity, ASP.NET Core Minimal APIs, and Aspire-ready composition. |
| III. Minimal API Vertical Slices | PASS | The only new persisted user setting is represented as a ThemePreferences vertical slice; no MVC pattern is introduced. |
| IV. PostgreSQL with Dapper | PASS | Persistent preference storage is relational PostgreSQL accessed via Dapper and SQL files in the database project. |
| V. Container App Readiness | PASS | Quickstart and contracts use environment-driven services and browser standards suitable for containerized deployment. |

**Post-design gate**: PASS. No constitutional violations or unresolved clarifications.

## Complexity Tracking

No constitutional violations require justification.
