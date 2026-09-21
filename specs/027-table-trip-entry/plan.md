# Implementation Plan: Editable Itinerary Table View

**Branch**: `main` | **Date**: 2026-09-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/027-table-trip-entry/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Add a Table alternative beside the existing Timeline on trip details. The table
is the interactive counterpart of the printable itinerary: each trip leg is a
full-width group row and its tracked items occupy shared Type, Title, Location,
Start, End, Confirmation, and Estimated Cost columns beneath it. Refactor the
current print projection into an ID-bearing shared itinerary projection and
extract one semantic `TripItineraryTable` renderer used by both the interactive
trip page and `TripPrintDocument`. Optional callbacks add screen-only controls
that open the existing leg and tracked-item modals; no form, validation, API, or
persistence behavior is duplicated.

## Technical Context

**Language/Version**: C# 14 on .NET 10; Razor components; CSS

**Primary Dependencies**: Blazor Interactive Server, ASP.NET Core authorization, Bootstrap utilities, existing `ITripApiClient`, bUnit, xUnit

**Storage**: Existing PostgreSQL data via the unchanged trip-detail API; no schema or query change

**Testing**: xUnit and bUnit in `TripPlanner.Web.Tests`; existing Playwright-style E2E project for browser validation

**Target Platform**: Responsive authenticated web application; Chromium-class browsers; printable HTML; Linux containers on Azure Container Apps

**Project Type**: Distributed web application; this feature is a Blazor web-only vertical slice

**Performance Goals**: Switch between Timeline and Table without navigation or another API request; render a representative 25-leg/250-item trip without perceptible input delay; successful modal saves refresh both views without a full page reload

**Constraints**: One shared row renderer for screen and print; reuse existing create/edit modals and validation; retain semantic table markup; preserve all columns through contained horizontal scrolling; honor existing viewer/editor/owner permissions; no new API or database surface

**Scale/Scope**: One trip-detail page, one print document wrapper, one shared table component, one transient projection, existing leg/item modals, and focused component/unit/E2E coverage

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-research gate | Post-design gate |
|-----------|-------------------|------------------|
| I. Trip Planning Domain | PASS: the view directly organizes trip legs and their tracked items. | PASS: the UI contract preserves itinerary grouping, unassigned items, and existing domain rules. |
| II. .NET Application Stack | PASS: work remains in the existing .NET 10 Blazor application. | PASS: design uses Razor components and existing Aspire-hosted services only. |
| III. Minimal API Vertical Slices | PASS: no API change is proposed. | PASS: existing trip-detail and mutation endpoints remain unchanged. |
| IV. PostgreSQL with Dapper | PASS: no persistence change is proposed. | PASS: the shared projection is transient and introduces no alternate data-access path. |
| V. Container App Readiness | PASS: no runtime dependency or local-only assumption is introduced. | PASS: the feature is static server-rendered UI behavior within the existing container. |
| Technology Constraints | PASS: the current Blazor/API/PostgreSQL architecture is preserved. | PASS: no unused infrastructure or additional project is introduced. |
| Development Workflow | PASS: projection, renderer, page integration, and tests are independently verifiable. | PASS: quickstart and UI contract define focused validation slices. |

**Gate result**: PASS before research and PASS after design. No constitutional violations require justification.

## Project Structure

### Documentation (this feature)

```text
specs/027-table-trip-entry/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/
│   └── itinerary-table-ui.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/TripPlanner.Web/
├── Components/
│   ├── Pages/Trips/
│   │   └── TripDetails.razor             # Timeline/Table selector and existing modal wiring
│   └── Trips/
│       ├── TripItineraryTable.razor      # Shared semantic renderer for screen and print
│       └── TripPrintDocument.razor       # Metadata wrapper consuming shared renderer
├── Features/Trips/
│   └── TripPrintFormatting.cs            # Refactor to shared ID-bearing itinerary projection
└── wwwroot/css/
    └── app.css                            # Shared table, interaction, overflow, and print styles

tests/TripPlanner.Web.Tests/
├── Trips/
│   ├── TripItineraryTableTests.cs        # Hierarchy, callbacks, permissions, semantics
│   ├── TripPrintDocumentTests.cs         # Print wrapper and shared-renderer parity
│   └── TripDetailsTableViewTests.cs      # View selector and existing-modal integration
└── Features/Trips/
    └── TripItineraryProjectionTests.cs   # Ordering, grouping, IDs, formatting

tests/TripPlanner.E2E.Tests/
└── TripTableViewFlowTests.cs              # Responsive and keyboard workflow evidence
```

**Structure Decision**: Keep the feature entirely in `TripPlanner.Web`. The
existing trip-detail response already contains every displayed field and record
identifier. `TripItineraryTable` becomes the only leg/item row renderer;
`TripDetails` supplies optional interactive callbacks, while
`TripPrintDocument` supplies trip metadata and renders the same table without
callbacks. Existing API, contracts, database, and form components remain intact.

## Complexity Tracking

No violations.
