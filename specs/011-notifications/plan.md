# Implementation Plan: User Notifications

**Branch**: `main` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/011-notifications/spec.md`, plus planning detail: show unread count first in the user's account dropdown trigger as a number inside a circle; add Notifications as the third dropdown option with the same counter; route that option to a notification display screen; support awareness notifications that can be deleted; support actionable notifications that can be completed and deleted; record and display completion date/time and completing person; support person-only notifications and person-plus-trip notifications; show a trip link for trip-related notifications with current access validation.

## Summary

Add first-party user notifications across the existing Blazor, Minimal API, PostgreSQL/Dapper, and contracts projects. The implementation should add notification persistence, recipient-scoped API endpoints, account-dropdown unread count UI, a `/notifications` display screen, awareness/actionable notification list behavior, action completion metadata, soft deletion, optional trip links, notification preferences, and email outbox delivery that never blocks in-app delivery.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for authentication/token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3, and an email sender/outbox abstraction behind environment-driven configuration.

**Storage**: PostgreSQL through existing Dapper repositories and SQL files. Add notification schema/scripts for `notifications`, notification preferences, and email delivery/outbox state. Use indexed recipient/count/list queries and `(recipient_user_id, source_event_key)` uniqueness for duplicate suppression.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. This feature needs database tests for recipient filtering/counts/listing/completion/deletion/preferences, API tests for recipient-only access and trip-link access behavior, Blazor component tests for dropdown badge/menu and notification list states, and E2E flows for count navigation, awareness delete, actionable complete/delete, trip-related links, and person-only notifications.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Unread count should be fetched with a single indexed recipient query suitable for rendering in the shared account menu. Notification list should page newest-first and avoid N+1 trip lookups. Completing, reading, and deleting a notification should update one recipient-owned notification row. Email dispatch should be asynchronous/outbox-backed so notification creation is not delayed by transport latency.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, concise Program.cs setup through extension methods, environment-driven configuration, authenticated access, no hardcoded email credentials, and server-side recipient/trip authorization for all notification operations. The account dropdown count and Notifications option must be implemented in the existing authenticated navigation surface.

**Scale/Scope**: One new notifications vertical slice across contracts, API, database, and Blazor UI. Scope includes in-app list/count, account dropdown badge, notification display screen, awareness/actionable presentation, completion metadata, deletion, optional trip relationships, email delivery requests, and category/channel preferences. Browser push, mobile push, digest email, localization, quiet hours, advanced notification scheduling, and global event analytics are out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Notifications support trip planning awareness, trip-related links, shared-trip activity, and person-specific action items. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire, and authenticated ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | Notification endpoints are planned as Minimal API vertical slices with colocated request/handler code. |
| IV. PostgreSQL with Dapper | PASS | Notification persistence is planned through PostgreSQL schema scripts, Dapper repositories, and SQL files in the database project. |
| V. Container App Readiness | PASS | Email and database configuration remain environment-driven with no hardcoded credentials or local-only assumptions. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/011-notifications/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api.md
│   └── ui.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   └── Notifications/
│       └── NotificationContracts.cs
├── TripPlanner.Api/
│   ├── Features/
│   │   ├── Notifications/
│   │   └── Trips/
│   └── Extensions/
├── TripPlanner.Database/
│   ├── Notifications/
│   │   ├── INotificationRepository.cs
│   │   └── NotificationRepository.cs
│   └── Scripts/
│       ├── Schema/
│       ├── Queries/Notifications/
│       └── Commands/Notifications/
└── TripPlanner.Web/
    ├── Components/
    │   ├── Layout/
    │   │   └── NavMenu.razor
    │   └── Pages/Notifications/
    │       └── NotificationsIndex.razor
    └── Features/Notifications/
        └── NotificationApiClient.cs

tests/
├── TripPlanner.Api.Tests/
├── TripPlanner.Database.Tests/
├── TripPlanner.Web.Tests/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. Notification persistence belongs in `TripPlanner.Database`, recipient-scoped endpoints and trip-access validation belong in `TripPlanner.Api`, DTOs/enums belong in `TripPlanner.Contracts`, and the account dropdown badge plus notification page belong in `TripPlanner.Web`. Avoid a new notification service project; the vertical slice can use repository abstractions and a small email outbox/dispatcher abstraction inside the existing projects.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is recipient-owned notification rows, optional trip targeting, completion metadata on actionable notifications, soft deletion, a dropdown badge/menu entry in `NavMenu.razor`, a dedicated `/notifications` page, Minimal API endpoints for counts/list/read/delete/complete/preferences, asynchronous email outbox delivery, and category/channel preferences.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [contracts/ui.md](./contracts/ui.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Design supports trip-planning awareness and actionable trip/person notification workflows. |
| II. .NET Application Stack | PASS | Design uses existing Blazor, Minimal API, Aspire, contracts, and .NET test projects. |
| III. Minimal API Vertical Slices | PASS | Notification count/list/read/delete/complete/preference operations are bounded API slices; no MVC or controller refactor is introduced. |
| IV. PostgreSQL with Dapper | PASS | The data model uses SQL schema/scripts and Dapper repositories rather than Entity Framework. |
| V. Container App Readiness | PASS | Email dispatch and configuration are planned through environment-driven settings and asynchronous delivery state. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
