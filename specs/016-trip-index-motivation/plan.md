# Implementation Plan: Trip Index Motivation

**Branch**: `016-trip-index-motivation` | **Date**: 2026-07-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-trip-index-motivation/spec.md`

## Summary

Enhance the authenticated Trips index page so it feels more useful and motivating when empty or sparse. The plan replaces the current plain header sentence with warmer trip-planning copy and adds a small set of curated travel motivational facts near the empty/sparse state while preserving existing trip listing, sharing badges, pagination, loading, and error behavior.

The implementation approach is a narrow Blazor web slice: update `TripsIndex.razor` and/or `NoTripsEmptyState.razor`, add static fact content in the web layer, and extend existing bUnit tests for copy, empty-state branding, and owned/shared trip cards. No API, database, persisted preference, or external content feed is required.

## Technical Context

**Language/Version**: C# on .NET 10

**Primary Dependencies**: Blazor components in `TripPlanner.Web`, existing `ITripApiClient` trip list data, Bootstrap utility classes, bUnit/xUnit tests in `TripPlanner.Web.Tests`

**Storage**: N/A. Motivational facts are curated static web content for this feature; no PostgreSQL or persisted user settings.

**Testing**: Focused web component tests in `TripPlanner.Web.Tests`, especially existing brand and trip-sharing component tests around `NoTripsEmptyState` and `TripsIndex`

**Target Platform**: Authenticated Blazor web application surface, responsive across desktop, tablet, and phone browser viewports

**Project Type**: Web application front-end enhancement within the existing .NET solution

**Performance Goals**: Static copy/facts add no network calls and no measurable delay to trip list loading; rendering remains bounded to a small fixed fact set.

**Constraints**: Preserve existing authenticated trip listing behavior, error fallback, pagination, owned/shared badges, and create-trip action. Avoid outdated brand terms already covered by brand copy tests. Keep facts non-interactive unless a future implementation adds explicit actions.

**Scale/Scope**: One Trips index page, one reusable empty-state component if needed, at least three curated motivational facts, and focused tests for empty, sparse, and populated states.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Trip Planning Domain | PASS | The feature improves the trip planning index and motivates practical trip organization. |
| II. .NET Application Stack | PASS | Work stays in the existing .NET 10 Blazor web application. |
| III. Minimal API Vertical Slices | PASS | No API changes are required; existing trip list API behavior is preserved. |
| IV. PostgreSQL with Dapper | PASS | No persistence change is introduced, so the database architecture remains untouched. |
| V. Container App Readiness | PASS | Static web content introduces no local-only services or deployment assumptions. |

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check**: PASS — Phase 1 design keeps all new behavior in Blazor UI/components and web tests, with no new framework, storage, API, or infrastructure concerns.

## Project Structure

### Documentation (this feature)

```text
specs/016-trip-index-motivation/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 decisions
├── data-model.md        # Phase 1 content/state model
├── quickstart.md        # Phase 1 validation guide
├── contracts/
│   └── ui.md            # Trips index UI behavior contract
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
└── TripPlanner.Web/
    ├── Components/Pages/Trips/
    │   └── TripsIndex.razor          # Header copy, sparse-state placement, existing list behavior
    └── Components/Trips/
        └── NoTripsEmptyState.razor   # Empty-state motivation/fact presentation if colocated there

tests/
└── TripPlanner.Web.Tests/
    ├── Brand/                        # Brand copy and empty-state copy contracts
    └── TripSharing/                  # Existing TripsIndex owned/shared badge coverage plus sparse-state checks
```

**Structure Decision**: This is a front-end-only Blazor enhancement. Keep copy and fact presentation close to the Trips index/empty-state components, because the content is static, page-specific, and does not need a cross-layer contract or persistence boundary.

## Complexity Tracking

> No constitution violations. Section intentionally left empty.
