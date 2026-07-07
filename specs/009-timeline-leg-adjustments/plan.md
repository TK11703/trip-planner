# Implementation Plan: Trip Leg and Event Timeline Adjustments

**Branch**: `main` | **Date**: 2026-07-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/009-timeline-leg-adjustments/spec.md`

## Summary

Enhance the existing trip timeline so each sticky trip-leg label shows an event count, each leg row exposes an easy add-event action, event bars no longer consume the full row height and block time-slot clicks, and trip-leg time-range bands remain visually obvious in dark mode. The implementation should stay within the current first-party Blazor timeline component and timeline projection rather than introducing a new calendar package or persistence model.

## Technical Context

**Language/Version**: C# on .NET 10 for the Blazor Web App, Minimal API, shared contracts, database access, and tests.

**Primary Dependencies**: ASP.NET Core, Blazor Web App with server-side interactivity, Microsoft Identity Web for Azure/Entra authentication and token acquisition, Minimal APIs, Aspire local orchestration/composition, Npgsql, Dapper, PostgreSQL, Bootstrap 5.3, and first-party timeline JavaScript/CSS in `TripPlanner.Web`.

**Storage**: PostgreSQL through existing Dapper repositories and SQL files. No new storage is required because per-leg event counts can be derived from `TimelineLeg.Items.Count` in the existing timeline response.

**Testing**: xUnit test projects for API, database, web component, and E2E coverage. This feature primarily needs Blazor component/UI tests and focused E2E coverage for timeline row counts, add-from-leg behavior, dark mode contrast/readability, and lane clickability when event bars overlap the leg time range.

**Target Platform**: Container-ready ASP.NET Core services suitable for Azure Container Apps, with Aspire used locally for orchestration and environment-driven configuration.

**Project Type**: Web application with a Blazor Web App front end, authenticated Minimal API middle tier, dedicated PostgreSQL/Dapper database project, shared contracts project, and Aspire app host/service defaults.

**Performance Goals**: Timeline rendering should remain immediate for normal trip sizes. Event counts must be computed from already-loaded timeline data without extra per-leg API calls. Row click targets must remain responsive and should not require reinitializing the timeline JavaScript on every pointer interaction.

**Constraints**: Preserve .NET 10, Blazor Web App, Minimal API vertical slices, PostgreSQL with Dapper, SQL files in the database project, Aspire composition, Bootstrap 5.3, no MVC, no Entity Framework, no jQuery, concise Program.cs setup through extension methods, environment-driven configuration, and authenticated owner-scoped access. Timeline item bars must not cover the entire row height; the row must retain a visible and clickable lane area for selecting a time within the leg. The sticky left trip-leg label should show `# events` below each leg title and location summary.

**Scale/Scope**: One existing timeline component (`TripTimeline.razor`), its CSS/JS support, the trip detail parent callback flow, and timeline-focused tests. Scope includes count display, add-event affordance from the leg row, event-bar height/position adjustments, dark-mode leg-band styling, and validation that lane clicks still create the intended slot selection. New event types, drag/drop scheduling, bulk editing, and timeline virtualization are out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Timeline counts, add-event flow, and clearer leg ranges directly improve itinerary planning around trip legs and events. |
| II. .NET Application Stack | PASS | Plan stays on C#/.NET 10, Blazor Web App, Aspire, and container-ready ASP.NET Core services. |
| III. Minimal API Vertical Slices | PASS | No new API slice is required; existing timeline and tracked item endpoints remain the boundary. |
| IV. PostgreSQL with Dapper | PASS | No schema change is required; any data access remains through existing Dapper timeline/tracked-item surfaces. |
| V. Container App Readiness | PASS | UI/CSS behavior and derived counts introduce no local-only deployment assumptions. |

**Pre-design gate**: PASS. No constitutional violations or unresolved technical clarifications.

## Project Structure

### Documentation (this feature)

```text
specs/009-timeline-leg-adjustments/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── ui.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Web/
│   ├── Components/
│   │   ├── Pages/Trips/TripDetails.razor
│   │   └── Timeline/TripTimeline.razor
│   └── wwwroot/
│       ├── css/app.css
│       └── js/tripTimeline.js
├── TripPlanner.Contracts/
│   └── Timeline/TimelineContracts.cs
├── TripPlanner.Api/
│   └── Features/Timeline/GetTripTimelineEndpoint.cs
└── TripPlanner.Database/
    ├── Timeline/TimelineRepository.cs
    └── Scripts/Queries/Timeline/GetTripTimeline.sql

tests/
├── TripPlanner.Web.Tests/
│   └── Timeline/
├── TripPlanner.Api.Tests/
│   └── Timeline/
├── TripPlanner.Database.Tests/
│   └── Timeline/
└── TripPlanner.E2E.Tests/
```

**Structure Decision**: Keep the existing Aspire-composed multi-project solution. The main implementation belongs in `TripPlanner.Web` because counts can be derived from existing timeline data and the clickability/visibility issues are presentation behavior. Contract/API/database work should remain limited to verification unless implementation discovers a missing timeline field.

## Phase 0: Research Summary

Research decisions are captured in [research.md](./research.md). The selected approach is to derive event counts from the existing `TimelineLeg.Items`, show the count in the sticky leg label as `# events`, keep add-from-leg inside the existing timeline/trip detail callback flow, reduce event bars to roughly half-row height so the lane remains clickable, and strengthen dark-mode leg-band tokens in CSS.

## Phase 1: Design Summary

Design artifacts are captured in:

- [data-model.md](./data-model.md)
- [contracts/ui.md](./contracts/ui.md)
- [quickstart.md](./quickstart.md)

## Post-Design Constitution Check

| Principle | Gate Result | Notes |
|-----------|-------------|-------|
| I. Trip Planning Domain | PASS | Design improves the trip timeline's usefulness for planning per-leg events. |
| II. .NET Application Stack | PASS | Design uses the established Blazor component and .NET project surfaces. |
| III. Minimal API Vertical Slices | PASS | No MVC or cross-cutting API refactor is introduced. |
| IV. PostgreSQL with Dapper | PASS | Derived counts avoid unnecessary schema/query duplication. |
| V. Container App Readiness | PASS | Design remains environment-independent and container-friendly. |

**Post-design gate**: PASS. No constitutional violations.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| None | N/A | N/A |
