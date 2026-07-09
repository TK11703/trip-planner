# Implementation Plan: Notification Preferences

**Branch**: `main` | **Date**: 2026-07-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-notification-preferences/spec.md`

## Summary

Consolidate notification preference management into the user profile experience while preserving existing notification preference data compatibility. Notification delivery must evaluate each intended recipient's preferences before any in-app or email delivery, and the concrete notification triggers are itinerary changes and trip sharing access changes. Itinerary-change notifications are generated for eligible viewers/editors other than the acting person; trip-sharing notifications are generated for the affected person after share, permission-change, or permission-removal success.

The implementation approach keeps the existing .NET 10 / Blazor / Minimal API / PostgreSQL architecture, extends profile contracts/UI to own preference editing, keeps Dapper SQL in the database project, and routes trip/sharing mutation endpoints through a notification orchestration path that resolves candidate recipients then applies profile-backed preferences before delivery.

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: ASP.NET Core Minimal APIs (`TripPlanner.Api`), Blazor (`TripPlanner.Web`), Dapper/Npgsql data access (`TripPlanner.Database`), shared DTO contracts (`TripPlanner.Contracts`), existing notification service/repository and trip-sharing access resolver

**Storage**: PostgreSQL. Existing `notification_preferences` data can remain as compatibility/detail storage, while profile contracts become the user-facing preference surface. User profile columns already contain notification preference fields and may need extension for the concrete categories.

**Testing**: .NET test projects: `TripPlanner.Api.Tests`, `TripPlanner.Database.Tests`, `TripPlanner.Web.Tests`, and targeted E2E validation in `TripPlanner.E2E.Tests` where UI behavior is covered

**Target Platform**: Blazor web application, Minimal API backend, and PostgreSQL database deployable as containers through Aspire/Azure Container Apps

**Project Type**: Web application with front end, API, contracts, and database projects

**Performance Goals**: Preference resolution and notification fan-out complete within the successful mutation request path without adding noticeable latency for typical trips; candidate recipient evaluation should be set-based or bounded by trip membership, not by unrelated users

**Constraints**: Preserve existing notification records and duplicate suppression behavior; do not notify the acting person for itinerary changes; apply preferences per recipient before delivery; generate notifications only after the underlying trip/share mutation succeeds; keep email failure isolated from the primary trip/share operation

**Scale/Scope**: One profile preference surface, two notification categories (`ItineraryChanges`, `TripSharing`), itinerary mutation triggers for trip edits, leg create/update/delete, and event create/update/delete, plus trip sharing add/change/remove triggers

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Trip Planning Domain | PASS | Notifications are tied to trip itinerary and sharing changes in the trip planning domain. |
| II. .NET Application Stack | PASS | Uses the existing .NET 10 backend, Blazor web app, and Aspire-compatible solution. |
| III. Minimal API Vertical Slices | PASS | Profile, notifications, trip items, trips, and trip sharing remain Minimal API vertical slices with endpoint-local behavior and shared services. |
| IV. PostgreSQL with Dapper | PASS | Preference and notification persistence remain in PostgreSQL with Dapper SQL files in `TripPlanner.Database`. |
| V. Container App Readiness | PASS | No local-only services or non-container assumptions are introduced; email remains outbox-backed. |

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check**: PASS — Phase 1 design keeps contracts in shared DTOs, data access in the database project, UI in Blazor profile screens, and notification orchestration in existing API/service boundaries. No new infrastructure, framework, or persistence pattern is introduced.

## Project Structure

### Documentation (this feature)

```text
specs/015-notification-preferences/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 decisions
├── data-model.md        # Phase 1 data design
├── quickstart.md        # Phase 1 validation guide
├── contracts/
│   └── api.md           # Profile preference and notification-trigger contracts
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   ├── Profile/
│   │   └── UserProfileContracts.cs        # Profile-owned notification preferences
│   └── Notifications/
│       └── NotificationContracts.cs       # Category definitions and delivery DTO compatibility
├── TripPlanner.Api/
│   ├── Features/UserProfiles/             # Profile GET/PUT preference surface
│   ├── Features/Notifications/            # Preference resolution + notification service behavior
│   ├── Features/Trips/UpdateTrip/         # Itinerary trigger: trip edit
│   ├── Features/TripItems/                # Itinerary triggers: leg/event create/update/delete
│   └── Features/TripSharing/              # Sharing triggers: share/change/remove
├── TripPlanner.Database/
│   ├── Scripts/Schema/                    # Profile/preference schema compatibility
│   ├── Scripts/Commands/UserProfiles/     # Profile preference updates
│   ├── Scripts/Queries/UserProfiles/      # Profile preference reads
│   ├── Scripts/Queries/Notifications/     # Existing preference compatibility reads if retained
│   └── Notifications/                     # Repository/service data records
└── TripPlanner.Web/
    └── Components/ or Pages/Profile/      # Profile UI with notification preference controls

tests/
├── TripPlanner.Api.Tests/                 # Endpoint/service tests for preference enforcement and triggers
├── TripPlanner.Database.Tests/            # SQL/repository tests for profile preference persistence/compatibility
├── TripPlanner.Web.Tests/                 # Profile UI preference editing tests
└── TripPlanner.E2E.Tests/                 # End-to-end preference override and notification trigger scenarios
```

**Structure Decision**: Web application with coordinated API, contracts, database, and Blazor changes. The profile feature owns the user-facing preference surface; the notification feature owns delivery evaluation; trip and trip-sharing mutation endpoints emit candidate notification events only after successful mutations. Existing `notification_preferences` compatibility is handled behind repository/service boundaries so callers use profile-backed preference contracts.

## Complexity Tracking

> No constitution violations. Section intentionally left empty.
