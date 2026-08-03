# Implementation Plan: Trip Leg Item Terminology

**Branch**: `023-trip-item-terminology` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/023-trip-item-terminology/spec.md`

## Summary

Rename the generic "event" concept to **item** across the whole stack, because "event" is also one of four values in the item-type field and the collision makes the product read as if every reservation were an event.

The approach is a mechanical, behavior-preserving rename with three engineered parts and one gate:

1. **Database** — `parsed_event_drafts` becomes `parsed_item_drafts`. Because the schema system re-runs every script on every start with no migration tracking, the rename is expressed as an in-place edit of `010_email_ingestion.sql` **plus** a guarded reconciling script `012` that backfills legacy rows and drops the old table. Any simpler approach either loses data or leaves a stray table forever (research R-002, R-003).
2. **Contracts and code** — literal `Event` → `Item` substitution across the email-ingestion contracts, repository, recognizer, and notification kinds. Two JSON field names change; no routes change. Breaking the contract is accepted because the API has no external consumers.
3. **Traveler-facing text** — roughly 45 strings across timeline, trip details, item form, validation, notifications, email review, printable trip, and FAQ.

Scope is deliberately narrowed: only the *trip leg child* sense of "event" is renamed. Security audit events and notification deduplication keys are genuinely events and stay (research R-001).

Completion is proved by a repository-wide search gate rather than by diff review, because the change touches ~143 occurrences across seven projects.

## Technical Context

**Language/Version**: C# 14 on .NET 10

**Primary Dependencies**: Blazor (interactive server), ASP.NET Core Minimal APIs, .NET Aspire, Dapper, Npgsql, xUnit, bUnit

**Storage**: PostgreSQL. Schema applied by `DatabaseInitializer` executing every `.sql` in `Scripts/Schema/` in filename order on each start — desired-state, no migration tracking table

**Testing**: xUnit across four projects; bUnit for Blazor component tests. Projects must be tested one at a time (`dotnet test` rejects multiple project paths)

**Target Platform**: Linux containers on Azure Container Apps; local orchestration via Aspire AppHost

**Project Type**: Web application — Blazor front end, Minimal API middle tier, PostgreSQL backend, shared contracts library

**Performance Goals**: N/A. Pure rename with no algorithmic or query-shape change

**Constraints**:

- Zero behavior change (FR-012) — every flow must work identically after the rename
- Zero data loss (FR-010, FR-015) — the drafts table rename must preserve every row on existing databases
- No route changes (FR-011) — bookmarked and shared links keep resolving
- The `'event'` item-type value is immutable (FR-009, FR-016)
- The language model prompt and its deserialization envelope must change atomically, or parsing fails silently (research R-005)

**Scale/Scope**: ~143 occurrences across 7 projects — 2 database objects, 5 renamed SQL command/query files, 2 renamed schema files, 1 new schema file, ~17 C# types, ~18 methods and properties, ~45 traveler-facing strings, ~26 test methods, 1 CSS class

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
|-----------|-----------|---------|
| **I. Trip Planning Domain** | The feature exists to make the domain vocabulary accurate: a leg's children are items, of which Event is one type. It strengthens domain clarity rather than diluting it. Note: the constitution's own wording ("dated trip legs, and events, reservations, or activities") predates this correction and would read better as "items" — flagged, not blocking. | PASS |
| **II. .NET Application Stack** | No stack change. C# on .NET 10, Blazor, and Aspire are all unchanged. | PASS |
| **III. Minimal API Vertical Slices** | No structural change. Renames stay inside existing slices (`Features/TripItems`, `Features/EmailIngestion`, `Features/Notifications`). No endpoint moves between slices; no `Program.cs` growth. | PASS |
| **IV. PostgreSQL with Dapper** | No data-access pattern change. Dapper stays; SQL stays in `TripPlanner.Database/Scripts`. The new `012` script follows the existing idempotent-script convention. | PASS |
| **V. Container App Readiness** | No configuration change. The migration runs through the existing startup initializer, so containers self-heal on deploy with no manual step. | PASS |

**Post-design re-check**: PASS. Phase 1 introduced no new projects, no new dependencies, no new abstractions, and no new configuration. The only added artifact is one `.sql` file that matches the established convention.

**Development Workflow — small vertical slices**: satisfied by the six-step delivery order in research R-010. Each step after the foundation is independently shippable and independently testable.

## Project Structure

### Documentation (this feature)

```text
specs/023-trip-item-terminology/
├── plan.md                          # This file
├── spec.md                          # Feature specification
├── research.md                      # Phase 0 output — 10 decisions
├── data-model.md                    # Phase 1 output — authoritative rename map
├── quickstart.md                    # Phase 1 output — validation guide
├── contracts/
│   ├── email-ingestion-api.md       # The one breaking wire contract change
│   └── terminology-gate.md          # Mechanical definition of done
├── checklists/
│   └── requirements.md              # Specification quality checklist
└── tasks.md                         # Phase 2 output (/speckit.tasks — NOT created here)
```

### Source Code (repository root)

Files this feature touches, within the existing layout. No directories are added or moved.

```text
src/
├── TripPlanner.Contracts/
│   └── EmailIngestion/EmailIngestionContracts.cs        # 4 records, 2 properties renamed
├── TripPlanner.Database/
│   ├── EmailIngestion/
│   │   ├── IParsedEventDraftRepository.cs               # → IParsedItemDraftRepository.cs
│   │   └── ParsedEventDraftRepository.cs                # → ParsedItemDraftRepository.cs
│   └── Scripts/
│       ├── Schema/
│       │   ├── 005_trip_leg_events.sql                  # → 005_trip_leg_items.sql
│       │   ├── 006_event_detail_shortcuts.sql           # → 006_item_detail_shortcuts.sql
│       │   ├── 010_email_ingestion.sql                  # edited in place
│       │   └── 012_parsed_item_drafts_rename.sql        # NEW — reconciling migration
│       ├── Commands/EmailIngestion/                     # 3 files renamed
│       └── Queries/EmailIngestion/                      # 2 files renamed
├── TripPlanner.Api/
│   ├── Extensions/WebApplicationBuilderExtensions.cs    # DI registrations
│   └── Features/
│       ├── TripItems/                                   # validator + leg endpoint messages
│       ├── EmailIngestion/                              # recognizer types, LLM prompt, relay copy
│       └── Notifications/                               # ItineraryChangeKind values + templates
└── TripPlanner.Web/
    ├── Components/
    │   ├── Timeline/TripTimeline.razor                  # counts, add control, empty states
    │   ├── TripItems/TrackedItemForm.razor              # labels, help text, aria-label
    │   ├── Trips/                                       # print document, map modal
    │   └── Pages/                                       # TripDetails, InboxDrafts, Faq, About, Profile
    ├── Features/Trips/TripPrintFormatting.cs            # 2 methods, 2 properties
    └── wwwroot/css/app.css                              # .tp-print-event → .tp-print-item

tests/
├── TripPlanner.Api.Tests/                               # 2 test names
├── TripPlanner.Database.Tests/                          # 1 test name
└── TripPlanner.Web.Tests/                               # ~23 test names, 11 assertions
```

**Structure Decision**: Existing four-project source layout plus four test projects, unchanged. This feature adds no projects and moves no files between projects. The only structural additions are one new SQL script and seven file renames, all within directories that already exist.

## Complexity Tracking

No constitution violations. The one place added complexity is justified is the reconciling migration script `012`, and that complexity is forced by the existing schema system rather than introduced by this feature:

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Separate `012` reconciling script alongside an in-place edit of `010` | Scripts re-run on every start with no tracking table, so a rename must be correct for fresh databases, the first upgrade, and every subsequent start | Editing `010` alone strands existing data in an orphaned table; a `012`-only `ALTER TABLE ... RENAME` fails because `010` has already created the empty target in the same pass; leaving `010` untouched recreates a stray legacy table on every start thereafter |

## Spec Refinement Applied During Planning

Research R-001 established that "event" carries three unrelated senses in this codebase. FR-017 and SC-007 were originally written as absolutes ("the only occurrences of 'event' anywhere in the product"), which would have pulled the security audit tables and the notification deduplication key into scope. Both were narrowed in [spec.md](./spec.md) to the trip-leg-child sense, matching the stated rationale for the rename. No other spec change was made.
