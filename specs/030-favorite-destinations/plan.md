# Implementation Plan: Favorite Destinations

**Branch**: `main` | **Date**: 2026-09-28 | **Spec**: [spec.md](./spec.md)

**Input**: [Feature specification](./spec.md) and the planning arguments for Favorites navigation, CRUD, ordered/searchable listing, address parsing, JSON/CSV import, and tracked-item prefill.

## Summary

Add a private favorites area to the existing Blazor application. Signed-in travelers can create, review, search, edit, delete, and bulk-import favorite destinations. Each destination stores its traveler-entered name and address plus city/country components resolved from the existing Azure Maps integration when available. A favorite can be selected inside the existing tracked-item dialog to prefill reusable fields without creating a durable link to the favorite. Persistence and authorization remain in the existing Minimal API and PostgreSQL/Dapper layers.

## Technical Context

**Language/Version**: C# 14, .NET 10, Razor components and CSS.

**Primary Dependencies**: Existing ASP.NET Core Minimal API, Blazor Interactive Server, Aspire, Npgsql, Dapper, Bootstrap 5.3, Azure Maps Search through the existing `IPlaceSuggestionLookup` integration, System.Text.Json, and CsvHelper for standards-compliant CSV parsing. Add a stable supported CsvHelper version through central package management before implementation.

**Storage**: PostgreSQL through `TripPlanner.Database`; add an owner-scoped `favorite_destinations` table in schema migration 018.

**Testing**: Existing API/database xUnit projects, Web bUnit tests, and Playwright-backed E2E tests. Cover owner isolation, CRUD and duplicate confirmation, address-resolution outcomes, atomic JSON/CSV import, stable ordering and search, and tracked-item prefill independence.

**Target Platform**: Existing authenticated Blazor Web App and Minimal API, composed locally by Aspire and deployable as Linux containers to Azure Container Apps.

**Project Type**: Existing distributed web application with Web, Contracts, API, Database, ServiceDefaults, and AppHost projects.

**Performance Goals**: A traveler with 500 saved destinations can open a usable list within 2 seconds. Search and ordering are performed in the API/database query so results remain consistent.

**Constraints**: Favorites are private to the authenticated owner. Owner identity comes only from validated authentication, not request data. Preserve .NET 10, Blazor, Minimal API vertical slices, PostgreSQL/Dapper, Aspire, and existing trip-item authorization, schedule, and leg rules. Address parsing must not make saving depend on Azure Maps availability. Import validation is all-or-nothing. No application code is changed as part of this planning workflow.

**Scale/Scope**: One favorites vertical slice across existing projects. The feature owns personal research records only; it does not add favorite sharing, public discovery, ratings, images, trip-item synchronization, or a new compute service.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-research gate | Post-design gate |
|---|---|---|
| I. Trip Planning Domain | PASS: Favorites support trip research and reuse during itinerary creation. | PASS: Favorites remain independent until copied into the existing tracked-item flow. |
| II. .NET Application Stack | PASS: The current .NET 10, Blazor, and Aspire stack is retained. | PASS: No new platform or service is introduced. |
| III. Minimal API Vertical Slices | PASS: CRUD, import, and address resolution fit the existing API feature organization. | PASS: Favorite routes, validators, DTOs, and handlers form a bounded vertical slice. |
| IV. PostgreSQL with Dapper | PASS: Favorite persistence belongs in the database project. | PASS: A versioned SQL migration and Dapper repository are used; no ORM is added. |
| V. Container App Readiness | PASS: The design uses existing environment-driven configuration and services. | PASS: Azure Maps remains an optional existing integration; the feature degrades without it. |
| Development Workflow | PASS: Web/API/database behavior can be tested independently. | PASS: API, UI, import, and end-to-end scenarios are defined in contracts and quickstart. |

**Gate result**: PASS before research and PASS after design. No constitutional violations.

## Architecture Decisions

- Add a signed-in Favorites link to the existing account dropdown and a `/favorites` page. Keep favorite CRUD in a dedicated `FavoriteDestinations` API/database feature slice.
- Derive `owner_user_id` from the authenticated caller for every operation. Filter by owner in SQL for reads, updates, deletes, duplicate checks, and import. Return the same not-found behavior for another user's IDs as for nonexistent IDs.
- Persist the entered address as the canonical location. Calculate city, country, latitude, and longitude from a structured Azure Maps Search result through the existing authenticated Maps client; these are not client-editable. Clear and recalculate them when an update changes the address. If Maps is unavailable or returns no reliable result, preserve the address and save with those values blank.
- Keep search server-side and case-insensitive across name, address, city, country, and notes. Sort by country, city, then name, with empty components last and a stable ID tie-breaker. Keep one sticky search input while the results scroll.
- Parse JSON with System.Text.Json and CSV with CsvHelper. Validate the complete file before writing, report row-specific errors, request explicit duplicate confirmation, and commit accepted rows in one database transaction. No partial rows are written.
- Add a favorite selector to the existing tracked-item form. Selecting a favorite copies title, address/location, and notes into the form; it does not change trip-specific values or save/synchronize the favorite. Existing item validation and trip access rules remain authoritative.
- Keep favorites out of trip deletion and trip-item foreign-key relationships. Removing or editing a favorite must not alter items created from its values.

## Project Structure

### Documentation (this feature)

```text
specs/030-favorite-destinations/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── api.md
    └── ui.md
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   └── FavoriteDestinations/
├── TripPlanner.Api/
│   └── Features/FavoriteDestinations/
├── TripPlanner.Database/
│   ├── FavoriteDestinations/
│   └── Scripts/
│       ├── Schema/018_favorite_destinations.sql
│       └── Queries/FavoriteDestinations/
└── TripPlanner.Web/
    ├── Components/Layout/NavMenu.razor
    ├── Components/Pages/Favorites.razor
    ├── Components/Favorites/
    ├── Components/TripItems/TrackedItemForm.razor
    └── Features/FavoriteDestinations/

tests/
├── TripPlanner.Api.Tests/FavoriteDestinations/
├── TripPlanner.Database.Tests/FavoriteDestinations/
├── TripPlanner.Web.Tests/FavoriteDestinations/
└── TripPlanner.E2E.Tests/FavoriteDestinations/
```

**Structure Decision**: Extend the existing multi-project solution. Contracts define shared favorite DTOs; API owns authorization, validation, address resolution, and import orchestration; Database owns migration and Dapper queries; Web owns the Favorites page and tracked-item selector. No new project or infrastructure resource is required.

## Phase 0: Research Summary

Decisions and alternatives are documented in [research.md](./research.md). Existing code confirms that the account dropdown is in `NavMenu.razor`, item creation is centralized in `TrackedItemForm.razor`, address lookup already uses Azure Maps, and the system uses owner-scoped Minimal API/Dapper vertical slices. Address component resolution must extend that boundary because its current public suggestion contract contains only a formatted string.

## Phase 1: Design Summary

- [data-model.md](./data-model.md) defines the owner-scoped favorite record, parsed fields, import unit, and invariants.
- [contracts/api.md](./contracts/api.md) defines CRUD, search, duplicate confirmation, import, and authorization behavior.
- [contracts/ui.md](./contracts/ui.md) defines navigation, sticky search, CRUD, import, and tracked-item prefill behavior.
- [quickstart.md](./quickstart.md) defines focused build and acceptance validation scenarios.

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|---|---|---|
| I. Trip Planning Domain | PASS | Favorites support research and reusable trip-planning details. |
| II. .NET Application Stack | PASS | Uses existing .NET, Blazor, and Aspire projects. |
| III. Minimal API Vertical Slices | PASS | New operations are isolated in the favorite feature slice. |
| IV. PostgreSQL with Dapper | PASS | Persistence uses versioned SQL and Dapper. |
| V. Container App Readiness | PASS | No local-only service dependency; Maps lookup failure is non-blocking. |
| Development Workflow | PASS | Focused test coverage and runnable validation are specified. |

**Post-design gate**: PASS. No constitutional violations or unresolved product clarifications.

## Complexity Tracking

No violations.
