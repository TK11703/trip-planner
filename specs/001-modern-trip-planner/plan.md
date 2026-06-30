# Implementation Plan: Modern Trip Planner

**Branch**: `tk11703-fill-constitution` | **Date**: 2026-06-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-modern-trip-planner/spec.md`

## Summary

Build an initial modern trip planning web application that lets Azure Entra-authenticated travelers manage only their own trips, view recent trips from a polished responsive landing page, create trips with valid date ranges, and review dated trip legs/tracked items on a calendar timeline. The implementation will use a .NET 10 Aspire solution with a Blazor Web App front end using server-side interactivity and OIDC, a C# Minimal API middle tier protected for authenticated API access, PostgreSQL persisted through Dapper, SQL files in a dedicated database project, Bootstrap 5.3 for responsive styling, vanilla JavaScript only where needed, and fullcalendar.io for the itinerary timeline.

## Technical Context

**Language/Version**: C# on .NET 10 for web, API, database abstractions, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web/OpenID Connect middleware for Azure Entra login, Minimal APIs, Aspire for local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3, fullcalendar.io, vanilla JavaScript for small browser interop only.

**Storage**: PostgreSQL. Dapper maps database records to DTOs/models. SQL statements and schema scripts live as `.sql` files in a dedicated database project rather than embedded SQL strings.

**Testing**: .NET test projects using xUnit for unit/integration tests, bUnit for Blazor component behavior, ASP.NET Core `WebApplicationFactory` for Minimal API contract tests, Playwright for key browser flows, and Testcontainers or Aspire-managed PostgreSQL for database-backed integration tests.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and composition.

**Project Type**: Web application with a Blazor front end, authenticated Minimal API middle tier, dedicated database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Initial implementation should return recent trips and trip details quickly for normal personal-use datasets, keep landing/detail pages responsive on mobile and desktop, and avoid client-side custom calendar rendering by using fullcalendar.io.

**Constraints**: Must use Blazor, Minimal APIs, PostgreSQL with Dapper, SQL files in the database project, vertical slices with requests/DTOs/handlers colocated by feature, Program.cs extension methods for setup, no MVC, no Entity Framework, no jQuery, environment-driven configuration, and container-ready components for Azure Container Apps.

**Scale/Scope**: Initial single-owner trip planning experience covering landing/recent trips, trip create, trip details, FAQ, about, trip legs/tracked item CRUD, authenticated API access, security/audit boundaries, and responsive calendar timeline. Shared trips, external calendar sync, airline/hotel/email import, and advanced collaboration are out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | The feature centers on itineraries, dated trip legs, events, reservations, activities, recent trips, and timeline visualization. |
| II. .NET Application Stack | PASS | Plan uses C#/.NET 10, Blazor Web App, and Aspire. |
| III. Minimal API Vertical Slices | PASS | API is planned as Minimal APIs with feature-colocated requests, DTOs, handlers, and endpoint mapping extensions. |
| IV. PostgreSQL with Dapper | PASS | PostgreSQL, Dapper, and SQL files in a dedicated database project are explicit decisions. |
| V. Container App Readiness | PASS | Components are environment-configured and container-ready for Azure Container Apps. |

**Pre-design gate**: PASS. No violations require justification.

## Project Structure

### Documentation (this feature)

```text
specs/001-modern-trip-planner/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api.md
│   └── ui-routes.md
└── tasks.md             # Generated later by /speckit.tasks
```

### Source Code (repository root)

```text
src/
├── TripPlanner.AppHost/                 # Aspire local orchestration
├── TripPlanner.ServiceDefaults/         # Shared Aspire service defaults
├── TripPlanner.Web/                     # Blazor Web App, OIDC, server-side interactivity
│   ├── Components/
│   │   ├── Layout/
│   │   ├── Pages/
│   │   └── Trips/
│   ├── Features/
│   │   ├── Home/
│   │   ├── Trips/
│   │   └── Timeline/
│   └── Extensions/                      # Program.cs middleware/auth/UI setup extensions
├── TripPlanner.Api/                     # Authenticated Minimal API middle tier
│   ├── Features/
│   │   ├── Trips/
│   │   ├── TripItems/
│   │   └── Audit/
│   └── Extensions/                      # Program.cs auth/endpoints/openapi setup extensions
├── TripPlanner.Contracts/               # Shared DTOs, request/response contracts
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

**Structure Decision**: Use a small Aspire-composed multi-project .NET solution. Web, API, contracts, and database concerns stay separate for deployment and testing, while API feature folders preserve vertical slices. The dedicated database project owns Dapper abstractions and `.sql` files to satisfy the constitution and keep SQL out of handlers.

## Security Boundaries

- **Front-end authentication**: `TripPlanner.Web` is the interactive OIDC client. It authenticates users with Azure Entra, protects personal pages, and presents sign-in/sign-out flows. FAQ/about remain public and must not load personal data.
- **API authentication**: `TripPlanner.Api` requires valid authenticated access for all personal trip endpoints. Anonymous requests to trip data return authorization failures with no personal data.
- **Ownership enforcement**: Every trip, leg, and tracked item query/mutation must include the signed-in user's immutable Entra subject/object identifier as a filter. Direct IDs or altered URLs must not bypass owner scoping.
- **Information disclosure**: Cross-user lookups should return a generic not-found/denied result without confirming whether another user's trip exists.
- **Token/data flow**: The Blazor server-side app calls the Minimal API with authenticated user context/tokens. API handlers treat front-end identity as untrusted unless validated by API authentication/authorization middleware.
- **Auditability**: Sensitive access and mutations record user identifier, operation, target resource identifier, result, and timestamp for maintainer security review without logging secrets or tokens.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). All technical choices and potential clarifications from the Technical Context have been resolved using the user-provided decisions and constitution constraints.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [contracts/ui-routes.md](./contracts/ui-routes.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Data model and contracts include trips, dated legs, tracked items, recent summaries, and timeline events. |
| II. .NET Application Stack | PASS | Design remains .NET 10, Blazor, ASP.NET Core, and Aspire-based. |
| III. Minimal API Vertical Slices | PASS | Contracts map to feature-owned Minimal API endpoints and handlers; no MVC dependency. |
| IV. PostgreSQL with Dapper | PASS | Data model is relational PostgreSQL; database project owns SQL scripts and Dapper mappings. |
| V. Container App Readiness | PASS | Quickstart and architecture use environment-driven configuration and Aspire composition ready for containerization. |

**Post-design gate**: PASS. No constitutional violations or unresolved clarifications.

## Complexity Tracking

No constitutional violations require justification.
