# Implementation Plan: Estimated Expenses

**Branch**: `main` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/017-estimated-expenses/spec.md`

## Summary

Add an optional **estimated cost** to each tracked item (event/activity/reservation/reminder) captured in the existing event detail modal form, then surface **estimated total** rollups without making expenses a primary focus. Totals are computed on demand: each trip leg shows an estimated total (sum of its items' estimated costs) in the travel leg column of the timeline, and the trip details page shows an overall estimated total that sums the estimated costs across all legs. The implementation extends the existing tracked item vertical slice, timeline projection, and trip detail contracts/UI rather than introducing a standalone expense concept. All user-facing wording uses the phrases "estimated cost" (per item) and "estimated total" (rollups) to make clear the amounts are estimates, not actuals.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Entra authentication and token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, and Bootstrap 5.3. Reuses the existing tracked item slice (contracts, validator, endpoints, SQL) and the timeline/trip-detail projections.

**Storage**: PostgreSQL. Add a single nullable `estimated_cost` money column to `tracked_items` via a new ordered schema script (`009_estimated_expenses.sql`) with a non-negative check constraint. No new tables: leg and trip estimated totals are derived (aggregated) values, not persisted. Access continues through Dapper repositories and SQL files in `src/TripPlanner.Database`.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. Existing infrastructure includes authenticated API tests, database-backed tests, Blazor component tests, and Playwright E2E tests.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Estimated total rollups add only lightweight aggregation over already-loaded item data. Event create/edit remains within normal modal response time; leg and trip totals render with no perceptible delay because they are computed from data already fetched for the timeline and trip detail.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, no jQuery, concise Program.cs setup through extension methods, environment-driven configuration, and authenticated owner-scoped access. User-facing wording MUST use "estimated cost" and "estimated total". Estimated cost MUST be optional and non-negative, stored with two-decimal precision. A missing estimate is distinct from an estimate of zero. Expenses MUST remain a secondary detail in the UI. A single, consistent display currency and formatting is used throughout; multi-currency entry/conversion is out of scope.

**Scale/Scope**: One new optional field on the event detail modal, one derived total per leg in the timeline leg column, and one derived overall total on the trip details page. Scope includes contracts, one database schema script, SQL projection/aggregation updates, repository mapping, API validation, Blazor form and display changes, and focused tests. Out of scope: expense categories, per-person splits, taxes/fees breakdowns, actual-vs-estimated reconciliation, multi-currency, and a standalone expense list or navigation area.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Estimated costs on itinerary events with leg and trip rollups directly support trip planning and budgeting awareness. |
| II. .NET Application Stack | PASS | Stays on C#/.NET 10, Blazor Web App, Aspire, and container-ready ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | Estimated cost extends the existing tracked item Minimal API slice; totals reuse the timeline and trip-detail slices. |
| IV. PostgreSQL with Dapper | PASS | Persistence is one nullable column added via an ordered SQL schema script; totals aggregate through SQL/Dapper without Entity Framework. |
| V. Container App Readiness | PASS | Estimated cost is environment-agnostic application data; totals are derived at query time with no local-only assumptions. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/017-estimated-expenses/
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
│   │   ├── Queries/Trips/
│   │   └── Schema/
│   ├── Timeline/
│   └── TripItems/
└── TripPlanner.Web/
  ├── Components/
  │   ├── TripItems/
  │   ├── Timeline/
  │   └── Pages/Trips/
  └── Features/
    └── Trips/

tests/
├── TripPlanner.Api.Tests/
│   └── TripItems/
├── TripPlanner.Database.Tests/
│   ├── TripItems/
│   └── Timeline/
├── TripPlanner.Web.Tests/
│   ├── TripItems/
│   └── Timeline/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. Estimated cost contract changes live in `TripPlanner.Contracts` (TripItems, Timeline, Trips). Create/update validation for the non-negative estimated cost lives in the existing TripItems API slice. The `estimated_cost` column, upsert mapping, and leg/trip aggregation live in `TripPlanner.Database`. The estimated cost input on the event modal, the per-leg estimated total in the timeline leg column, and the overall estimated total on the trip details page live in `TripPlanner.Web`.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is to store a single optional `estimated_cost` money value on `tracked_items`, treat missing as "no estimate" and zero as an explicit value, compute leg and trip estimated totals on demand from item values, reuse the tracked item validation slice for non-negative/precision rules, and present all amounts in one consistent display currency with the fixed wording "estimated cost" and "estimated total".

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Design keeps estimates attached to itinerary items and rolls them up into leg and trip planning views. |
| II. .NET Application Stack | PASS | Design uses the established .NET, Blazor, Minimal API, Aspire, and PostgreSQL surfaces. |
| III. Minimal API Vertical Slices | PASS | Create/update changes stay in the tracked item slice; totals are additive fields on existing timeline/trip projections. |
| IV. PostgreSQL with Dapper | PASS | Storage is one nullable column via an ordered schema script; aggregation uses SQL and Dapper mapping. |
| V. Container App Readiness | PASS | Estimated cost and derived totals are portable application data with no local-only dependencies. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
