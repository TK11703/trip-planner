# Implementation Plan: Travel Leg Modes and Item Eligibility

**Branch**: `main` | **Date**: 2026-08-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/025-leg-item-eligibility/spec.md`, amended by the planning input to include transportation modes and mode-dependent travel details.

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Persist an explicit `stay` or `travel` classification on every trip leg. Travel legs also persist one of `flight`, `train`, `bus`, `boat`, or `car`; all retain origin, destination, local start/end, and their time zones and may carry optional travel cost and confirmation details. Flight, train, bus, and boat cannot contain items. Car is the traveler-controlled exception and can contain items.

The change extends the existing contracts, Dapper mappings, leg form, timeline, print view, manual item validation, and email placement query. PostgreSQL migration `014` backfills existing origin-bearing legs as Travel/Car and other legs as Stay, then adds row checks and trigger-backed cross-table guards so an item assignment and a concurrent mode change cannot violate eligibility.

## Technical Context

**Language/Version**: C# on .NET 10; SQL for PostgreSQL

**Primary Dependencies**: Existing ASP.NET Core Minimal APIs, Blazor Web App, Dapper, Npgsql, Aspire, and xUnit/bUnit; no new packages

**Storage**: PostgreSQL. Migration `014_trip_leg_modes.sql` adds `leg_kind`, `transportation_mode`, `travel_cost`, and `confirmation_code` to `trip_legs`, backfills existing rows, and installs eligibility constraints/triggers.

**Testing**: xUnit API and database tests; bUnit form/timeline/print tests; existing solution build and focused project test runs

**Target Platform**: Linux containers on Azure Container Apps, locally orchestrated by Aspire; browser-hosted Blazor UI

**Project Type**: Web application with Blazor front end, Minimal API, shared contracts, and PostgreSQL data project

**Performance Goals**: Leg and item saves remain single-request operations; item eligibility adds only indexed primary-key lookups and no user-perceptible delay. Timeline and email placement remain one database round trip each.

**Constraints**: Existing data and item relationships must survive migration; restricted modes must be impossible to populate through any workflow or race; Stay has no travel-only data; current timezone and trip-range rules remain authoritative; no new infrastructure.

**Scale/Scope**: Four added leg fields, one migration, three shared contract projections, two API validators, three SQL query families, the leg form, timeline, print view, manual item picker, email review picker/matcher, and focused tests across four existing test projects.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
|---|---|---|
| I. Trip Planning Domain | Explicit transportation legs, booking details, and item eligibility strengthen the itinerary model. | PASS |
| II. .NET Application Stack | Uses the existing .NET 10, Blazor, and Aspire stack without additions. | PASS |
| III. Minimal API Vertical Slices | Behavior stays in `Features/TripItems` and the existing email-ingestion slice; endpoint registration remains unchanged. | PASS |
| IV. PostgreSQL with Dapper | Migration, queries, triggers, and mappings remain in `TripPlanner.Database`; no ORM is introduced. | PASS |
| V. Container App Readiness | No process-local state or new service is added; database-enforced invariants remain replica-safe. | PASS |

**Pre-Research Result**: PASS. No constitution violation requires justification.

**Post-Design Re-check**: PASS. The design adds no project, package, or infrastructure component. Cross-table eligibility is enforced in PostgreSQL, and application validation remains in the owning vertical slices.

## Project Structure

### Documentation (this feature)

```text
specs/025-leg-item-eligibility/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/
│   └── api.md            # Trip-leg and item-eligibility contracts
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   ├── TripItems/TripItemContracts.cs       # kinds, modes, request fields, eligibility helper
│   ├── Trips/TripContracts.cs               # leg detail fields
│   └── Timeline/TimelineContracts.cs        # leg mode/details and separate leg cost
├── TripPlanner.Api/Features/
│   ├── TripItems/
│   │   ├── TripLegValidator.cs              # kind/mode/booking validation
│   │   ├── TrackedItemValidator.cs          # selected-leg eligibility
│   │   └── TripLegEndpoints.cs              # mode-transition conflict handling
│   └── EmailIngestion/
│       └── DraftPlacementMatcher.cs         # consumes eligible candidate rows only
├── TripPlanner.Database/
│   ├── TripItems/TripItemRepository.cs      # new fields and transition result
│   ├── Timeline/TimelineRepository.cs       # leg projection and totals
│   └── Scripts/
│       ├── Schema/014_trip_leg_modes.sql
│       ├── Commands/TripLegs/UpsertAndDeleteTripLegs.sql
│       └── Queries/
│           ├── EmailIngestion/GetPlacementCandidateLegs.sql
│           └── Timeline/GetTripTimeline.sql
└── TripPlanner.Web/Components/
  ├── TripItems/TripLegForm.razor           # conditional fields and validation
  ├── TripItems/TrackedItemForm.razor       # eligible leg choices
  ├── EmailIngestion/DraftEditModal.razor   # eligible leg choices
  ├── Timeline/TripTimeline.razor           # mode/details and add-item gating
  └── Trips/TripPrintDocument.razor         # printable travel details

tests/
├── TripPlanner.Api.Tests/TripItems/
├── TripPlanner.Api.Tests/EmailIngestion/
├── TripPlanner.Database.Tests/
└── TripPlanner.Web.Tests/{TripItems,Timeline,Trips,EmailIngestion}/
```

**Structure Decision**: Extend the existing four-project web application and its current TripItems/EmailIngestion vertical slices. Shared enum-like string constants and eligibility logic live in contracts so API and Blazor agree; PostgreSQL remains the final authority for cross-row invariants.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitution violations.
