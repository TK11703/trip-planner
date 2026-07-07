# Implementation Plan: Trip Sharing and Collaboration

**Branch**: `main` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/010-trip-sharing/spec.md`, plus planning detail: add a Share button near Edit Trip, use a modal for current and new shares, pull selectable users from the Azure tenant when possible, keep trip edit/delete owner-only, badge owned vs shared trips on the trips page, and add a third shared-people column/card below trip legs.

## Summary

Add first-party trip sharing so a trip owner can grant tenant users either viewer or collaborator access, manage those shares from a Share modal on the trip detail page, and show owned/shared access states throughout the trip list and detail views. The implementation should add a PostgreSQL/Dapper `trip_shares` access model, an API-level trip access resolver used by trip/leg/item/timeline endpoints, owner-only share-management endpoints, a Microsoft Graph-backed tenant user lookup abstraction, and Blazor UI updates for the Share button, modal, access badges, and shared-people card.

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Azure/Entra authentication and token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3, and Microsoft Graph/Microsoft Graph SDK or Graph-compatible HTTP abstraction for tenant user search.

**Storage**: PostgreSQL through existing Dapper repositories and SQL files. Add a `trip_shares` table keyed by trip and member identity, plus indexes for trip-member uniqueness and shared-with-me trip listing.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. This feature needs database repository tests for share persistence/access queries, API authorization tests for owner/collaborator/viewer/no-access behavior, Blazor component tests for owner-only controls and badges, and E2E flows for sharing, access changes, and revocation.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Trip list and detail access checks should be handled with indexed joins, not per-trip N+1 lookups. Share modal tenant search should debounce/minimize calls, limit results, and avoid fetching broad tenant profile data. Revoked access must be enforced on the member's next API action.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, concise Program.cs setup through extension methods, environment-driven configuration, authenticated access, no hardcoded Azure credentials, least-privilege Microsoft Graph permissions, and server-side authorization for all access decisions. The latest user instruction makes trip metadata edit and trip delete owner-only; collaborators can edit itinerary content but cannot manage shares, edit trip metadata, or delete trips.

**Scale/Scope**: One new sharing vertical slice across contracts, API, database, and Blazor UI. Scope includes whole-trip sharing only, two member access levels (viewer/collaborator), owner-only share management, Azure tenant user lookup for selection, owned/shared badges on the trips page, and a shared-people card on trip detail. Notifications, ownership transfer, co-owners, per-leg/per-event sharing, and real-time conflict resolution are out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Sharing trips with viewers/collaborators directly supports group and partner itinerary planning. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire, and authenticated ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | New share and directory lookup endpoints are planned as Minimal API vertical slices with colocated requests/handlers. |
| IV. PostgreSQL with Dapper | PASS | Share persistence is planned through PostgreSQL schema scripts, Dapper repositories, and SQL files in the database project. |
| V. Container App Readiness | PASS | Graph and database configuration remain environment-driven with no hardcoded credentials or local-only assumptions. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/010-trip-sharing/
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
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
src/
├── TripPlanner.Contracts/
│   └── Trips/
│       └── TripContracts.cs
├── TripPlanner.Api/
│   ├── Features/
│   │   ├── Trips/
│   │   │   ├── GetTrips/
│   │   │   ├── GetTripDetail/
│   │   │   └── UpdateTrip/
│   │   ├── TripSharing/
│   │   └── TripItems/
│   └── Security/
├── TripPlanner.Database/
│   ├── Trips/
│   ├── TripSharing/
│   └── Scripts/
│       ├── Schema/
│       ├── Queries/
│       └── Commands/
└── TripPlanner.Web/
  ├── Components/
  │   └── Pages/Trips/
  │       ├── TripsIndex.razor
  │       └── TripDetails.razor
  └── Features/Trips/
    └── TripApiClient.cs

tests/
├── TripPlanner.Api.Tests/
├── TripPlanner.Database.Tests/
├── TripPlanner.Web.Tests/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. Share persistence belongs in `TripPlanner.Database`, share/access endpoints and authorization in `TripPlanner.Api`, shared DTOs/enums in `TripPlanner.Contracts`, and modal/list/detail badges in `TripPlanner.Web`. Avoid introducing a separate authorization service project; the access resolver can live in API/database layers as part of the trip-sharing vertical slice.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is to model trip sharing in PostgreSQL, centralize trip access resolution in the API, reserve share management/trip metadata/delete to owners, allow collaborators to edit itinerary content, back the share dialog's user search with a least-privilege Microsoft Graph tenant lookup abstraction, and expose caller access metadata through trip list/detail contracts.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/api.md](./contracts/api.md)
- [contracts/ui.md](./contracts/ui.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Design supports group trip planning by allowing owners to invite viewers/collaborators and display trip participants. |
| II. .NET Application Stack | PASS | Design uses existing Blazor, Minimal API, Aspire, contracts, and .NET test projects. |
| III. Minimal API Vertical Slices | PASS | Share and directory-user operations are bounded API slices; no MVC or cross-cutting controller refactor is introduced. |
| IV. PostgreSQL with Dapper | PASS | The new data model uses SQL schema/scripts and Dapper repositories rather than Entity Framework. |
| V. Container App Readiness | PASS | Azure tenant lookup and Graph credentials are planned through environment-driven configuration and managed/secure identity patterns. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
