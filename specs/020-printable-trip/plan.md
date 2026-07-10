# Implementation Plan: Printable Trip View

**Branch**: `020-printable-trip` | **Date**: 2026-07-10 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/020-printable-trip/spec.md`

## Summary

Give travelers a one-click, print-ready view of a single trip. On the trip details page the owner gets a **Print** button that opens a dedicated, chrome-free page at `/trips/{tripId}/print`. That page renders the trip as a printable HTML document and lets the traveler trigger the browser's native print dialog (which is also how a traveler saves a PDF). Layout order matches the request:

1. **Trip metadata first** — name, date range, description, and the estimated cost total.
2. **Legs in chronological order**, each rendered as a full-width **row divider** in the document table (leg title, origin → destination, and the leg's own start/end).
3. **Events within each leg** rendered as tabular rows with one column per collected field (type, title, location, start, end, confirmation code, estimated cost, notes).
4. **Datetime + timezone combined into a single column**: every `StartLocal`/`EndLocal` value is combined with its timezone into one cell formatted `MM/dd/yyyy HH:mm TZ` (for example `07/14/2026 09:30 America/New_York`). So "start" and "end" are two columns, each self-contained.

This is a **web-only vertical slice**. The existing `GET /api/trips/{tripId}` endpoint already returns a `TripDetail` carrying the trip metadata, all `Legs`, and all `TrackedItems` (with `StartLocal`, `StartTimeZoneId`, `EndLocal`, `EndTimeZoneId`, `EstimatedCost`, `ConfirmationCode`, `Notes`, `Location`). No API change, no contract change, and **no database change** are required. The work is a new routable print page, a chrome-free print layout, a small pure formatting helper (datetime+TZ, leg ordering, event grouping), a Print button on `TripDetails`, print-oriented CSS, and a tiny JS interop to invoke `window.print()`.

Ownership is enforced two ways: the print page only fetches through the same authenticated `ITripApiClient.GetDetailAsync` (which returns null for trips the caller cannot see), and the Print button surfaces only for owners on the trip page. A non-owner or missing trip yields the existing not-found/denied state, chrome-free.

## Technical Context

**Language/Version**: C# on .NET 10 (Blazor Web App, Interactive Server render mode; ASP.NET Core Minimal APIs already host the API tier)

**Primary Dependencies**: Blazor (`TripPlanner.Web`), existing `ITripApiClient` / `TripApiClient` (`GetDetailAsync`), shared DTOs `TripDetail` / `TripLegDto` / `TrackedItemDto` (`TripPlanner.Contracts`), `TimezoneOptions` (`TripPlanner.Contracts.Common`) for optional zone-token friendliness, Bootstrap 5 styling already bundled. New: one minimal JS helper for `window.print()`; no new NuGet or front-end libraries.

**Storage**: PostgreSQL — **unchanged**. No new tables, columns, or migrations. The print view is a read-only projection of data already persisted and already returned by `GET /api/trips/{tripId}`.

**Testing**: `TripPlanner.Web.Tests` (bUnit) for the print page — metadata block renders, legs appear in chronological order as row dividers, events render as rows grouped under their leg with one column per field, the combined datetime+TZ column formats as `MM/dd/yyyy HH:mm TZ`, empty-trip state, and not-found/denied state; plus a focused unit test of the pure `TripPrintFormatting` helpers (datetime+TZ formatting, leg ordering, event grouping/ordering, missing-optional handling). `TripPlanner.E2E.Tests` (Playwright) for: Print button on trip details opens the chrome-free print page for the owner, and the page contains the trip's legs/events without nav/footer chrome.

**Target Platform**: Modern evergreen browsers rendering the Blazor Web front end; native browser print / "Save as PDF". API and database remain container-deployable to Azure Container Apps via Aspire (untouched).

**Project Type**: Web application (Blazor front end + Minimal API + PostgreSQL + shared contracts). This feature lands almost entirely in `TripPlanner.Web`.

**Performance Goals**: Opening the print view issues a single existing `GET /api/trips/{tripId}` call and renders synchronously from the returned `TripDetail`; formatting is O(legs + events) with no per-frame work. Print itself is handled by the browser.

**Constraints**: The printable page MUST omit app chrome (NavMenu, footer, action buttons) via a dedicated layout so nothing interactive prints. Times MUST match the wall-clock values the traveler already sees (`StartLocal`/`EndLocal`), now made unambiguous by appending the timezone token. The page MUST paginate naturally across multiple printed pages for large trips (no fixed-height/overflow clipping; use print-friendly CSS with `table` row/`thead` repetition). Ownership restrictions MUST hold: rendering relies on the authenticated client and returns the existing denied state for non-owners. Content MUST be a semantically structured, labeled table so assistive tech reads it in order. Reuse existing light/dark theme tokens but bias the print stylesheet toward ink-friendly, high-contrast output under `@media print`.

**Scale/Scope**: One new routable page, one new chrome-free layout, one pure formatting helper class, one Print button addition on `TripDetails.razor`, one small JS interop function, and print CSS. A single trip at a time; typical trips have a handful of legs and up to a few dozen events.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Trip Planning Domain | PASS | Produces a printable itinerary of a trip's legs and events — squarely within trip planning. |
| II. .NET Application Stack | PASS | Blazor front end on .NET 10; Aspire orchestration untouched. No new stack elements; JS interop is a one-line `window.print()` helper. |
| III. Minimal API Vertical Slices | PASS | No new API surface is required; the feature reuses the existing `GET /api/trips/{tripId}` slice. If any server work were added later it would remain a Minimal API slice, but this plan adds none. |
| IV. PostgreSQL with Dapper | PASS | No data-access changes at all — read-only reuse of an existing endpoint; no EF introduced. |
| V. Container App Readiness | PASS | Pure front-end additions (page, layout, CSS, tiny JS). No new secrets, no local-only assumptions; remains container-deployable. |

**Result**: PASS — no violations. Complexity Tracking not required.

**Post-Design Re-check**: PASS — Phase 1 keeps all logic in `TripPlanner.Web` (page + layout + pure helper + CSS + minimal interop), reuses shared `TripDetail` contracts unchanged, adds no database or API surface, and enforces ownership through the existing authenticated client and owner-gated button. No new projects, no browser-exposed secrets, no persisted data.

## Project Structure

### Documentation (this feature)

```text
specs/020-printable-trip/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── print-page-route.md        # /trips/{tripId}/print route, layout, data source, states
│   ├── print-document-layout.md   # Metadata block + leg dividers + event columns contract
│   └── datetime-tz-format.md      # Combined "MM/dd/yyyy HH:mm TZ" column formatting rules
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
└── TripPlanner.Web/
    ├── Components/
    │   ├── Layout/
    │   │   └── PrintLayout.razor            # (new) chrome-free layout: renders @Body only, no NavMenu/footer
    │   ├── Pages/Trips/
    │   │   ├── TripDetails.razor            # Add owner-only "Print" button linking to /trips/{TripId}/print
    │   │   └── TripPrint.razor              # (new) @page "/trips/{TripId:guid}/print"; @layout PrintLayout;
    │   │                                    #        loads TripDetail, renders metadata + leg dividers + event rows,
    │   │                                    #        empty state, not-found/denied state, Print + Back actions
    │   └── Trips/
    │       └── TripPrintDocument.razor      # (new, optional) presentational table used by TripPrint (eases bUnit testing)
    ├── Features/Trips/
    │   └── TripPrintFormatting.cs           # (new) pure helpers: FormatDateTimeWithZone(StartLocal, zoneId),
    │                                        #        OrderLegsChronologically, GroupEventsByLeg, OrderEventsWithinLeg
    └── wwwroot/
        ├── js/
        │   └── tripPrint.js                 # (new) window.print() interop (invoked from the print page)
        └── css/
            └── app.css                      # Add @media print rules + .tp-print-* document/table styling

tests/
├── TripPlanner.Web.Tests/
│   └── Trips/
│       ├── TripPrintPageTests.cs           # (new) bUnit: metadata block, leg dividers in order, event columns,
│       │                                    #        combined datetime+TZ cell, empty + denied states
│       └── TripPrintFormattingTests.cs     # (new) unit: datetime+TZ formatting, ordering, grouping, missing optionals
└── TripPlanner.E2E.Tests/
    └── (add) PrintTripFlow                  # Playwright: owner clicks Print → chrome-free page with legs/events
```

**Structure Decision**: Web application. The feature is delivered as a single vertical slice inside `TripPlanner.Web`: a new routable print page (`TripPrint.razor`) under a new chrome-free layout (`PrintLayout.razor`), backed by a pure formatting helper (`TripPrintFormatting.cs`) for datetime+TZ formatting and leg/event ordering, with print CSS and a one-line `window.print()` interop. It reuses the existing `ITripApiClient.GetDetailAsync` → `TripDetail` (metadata + `Legs` + `TrackedItems`) with **no** changes to `TripPlanner.Api`, `TripPlanner.Contracts`, or `TripPlanner.Database`.

## Complexity Tracking

> No constitution violations — this section intentionally left empty.
