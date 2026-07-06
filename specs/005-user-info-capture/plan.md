# Implementation Plan: User Information Capture

**Branch**: `main` | **Date**: 2026-07-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/005-user-info-capture/spec.md`

## Summary

Add an authenticated user profile feature that persists traveler identity, contact, notification, and personalization information in PostgreSQL. On first authenticated Azure sign-in, Trip Planner creates the profile from available Azure/Entra claims for first name, last name, display name, and email address only when no saved profile exists. Returning users keep saved Trip Planner profile values, and a profile page lets users make fine-tuned adjustments without later Azure claim refreshes overwriting those edits.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Azure/Entra authentication and token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3.

**Storage**: PostgreSQL. User profile data extends the existing `users` table through ordered SQL schema scripts and is accessed through Dapper repositories with SQL files in `src/TripPlanner.Database`.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. Existing test infrastructure includes WebApplicationFactory-style API tests, database-backed tests, bUnit-style Blazor tests, and E2E tests.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Profile creation and profile page load should complete within normal authenticated page/API latency. Profile update flows should complete quickly enough for a user to save a single change in under one minute including interaction time.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, no jQuery, concise Program.cs setup through extension methods, environment-driven configuration, and authenticated owner-scoped access.

**Scale/Scope**: One profile per authenticated user. Initial scope includes Azure-seeded profile creation, profile retrieval, profile update, notification preferences, personalization preferences, validation, navigation to the profile page, and focused tests. Notification delivery is out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Profiles support trip-related notifications and personalization without changing core trip planning behavior. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire, and container-ready ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | Profile endpoints are planned as a dedicated Minimal API vertical slice with colocated request handling. |
| IV. PostgreSQL with Dapper | PASS | Profile persistence uses PostgreSQL, Dapper repositories, and SQL files in the database project. |
| V. Container App Readiness | PASS | Configuration remains environment-driven and compatible with container deployment. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/005-user-info-capture/
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
│   ├── Extensions/
│   ├── Features/
│   │   └── UserProfiles/
│   └── Security/
├── TripPlanner.Contracts/
│   └── Profile/
├── TripPlanner.Database/
│   ├── Scripts/
│   │   ├── Commands/UserProfiles/
│   │   ├── Queries/UserProfiles/
│   │   └── Schema/
│   └── UserProfiles/
└── TripPlanner.Web/
    ├── Components/
    │   ├── Layout/
    │   └── Pages/
    ├── Extensions/
    └── Features/
        └── Profile/

tests/
├── TripPlanner.Api.Tests/
│   └── UserProfiles/
├── TripPlanner.Database.Tests/
│   └── UserProfiles/
├── TripPlanner.Web.Tests/
│   └── Profile/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. Profile contracts live in `TripPlanner.Contracts`; authenticated profile endpoints live in `TripPlanner.Api/Features/UserProfiles`; Dapper persistence lives in `TripPlanner.Database/UserProfiles` with external SQL scripts; the editable profile UI and typed API client live in `TripPlanner.Web`.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is create-only Azure claim seeding with user-owned saved profile values as the source of truth after initial creation.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Data model supports user-owned trip personalization and notification preferences. |
| II. .NET Application Stack | PASS | Design uses the established .NET, Blazor, Minimal API, and Aspire stack. |
| III. Minimal API Vertical Slices | PASS | Profile functionality is isolated into a user profile API slice. |
| IV. PostgreSQL with Dapper | PASS | Persistence is modeled as SQL scripts and Dapper repositories. |
| V. Container App Readiness | PASS | No local-only assumptions are introduced. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

No constitutional violations require justification.
