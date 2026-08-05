# Implementation Plan: Matching Ingested Email Items to Trip Legs

**Branch**: `024-email-item-leg-matching` | **Date**: 2026-08-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/024-email-item-leg-matching/spec.md`

## Summary

Ingested email already produces parsed drafts, and the API already exposes an update endpoint that can set every draft field. What is missing is the middle: nothing ever suggests where a draft belongs, the review screen offers no way to edit or assign one, and confirmation writes straight to the database without running the item validator.

This plan closes that gap in four moves:

1. **Suggest placement at read time.** A new `DraftPlacementMatcher` compares each pending draft's timeframe against the travel windows of every leg on every trip the traveler can edit, and returns the result alongside the draft. Nothing is persisted, so a suggestion can never go stale and the traveler's stored choice is always their own.
2. **Make a leg optional but binding.** `TrackedItemValidator` stops requiring a leg and starts enforcing the travel window only when one is assigned. The database column is already nullable and the timeline already renders an "Unassigned" lane, so this is a validation and contract change rather than a schema one.
3. **Route confirmation through the validator.** `ConfirmDraftEndpoint` builds the same `CreateTrackedItemRequest` the item form builds and validates it identically, so the email path can no longer create an item the traveler could not have typed.
4. **Give the inbox a real editor.** A new `DraftEditModal` lets the traveler pick a trip, pick a leg (or none), and clean up any parsed field before saving — reusing the existing `UpdateDraftAsync` client method that has been sitting unused.

## Technical Context

**Language/Version**: C# 14 on .NET 10

**Primary Dependencies**: ASP.NET Core Minimal APIs, Blazor interactive server, Dapper, .NET Aspire, xUnit, bUnit

**Storage**: PostgreSQL. Raw SQL files in `TripPlanner.Database`; desired-state schema scripts replayed on every start, so all scripts must be idempotent

**Testing**: xUnit across `TripPlanner.Api.Tests` and `TripPlanner.Database.Tests`; bUnit in `TripPlanner.Web.Tests`; Playwright in `TripPlanner.E2E.Tests`

**Target Platform**: Linux containers, Azure Container Apps

**Project Type**: Web — Blazor front end, Minimal API middle tier, PostgreSQL backend

**Performance Goals**: Placement suggestions are computed during the existing draft-list request with no additional round trip (SC-008). One added query per list call, returning candidate legs for the caller's editable trips

**Constraints**:

- No background services in the API — feature 022 established this and `NoMailboxMonitoringTests` guards it. Matching must run inside a request
- Breaking contract and database changes are acceptable (confirmed by the product owner); no external consumers exist
- Schema scripts are desired-state and unversioned, so every new script must be safe to re-run
- Time comparisons must be instant-based, never wall-clock string comparisons (FR-002)

**Scale/Scope**: Tens of pending drafts per traveler; a trip carries a handful of legs. No pagination needed for matching

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
| --------- | ---------- | ------- |
| I. Trip Planning Domain | The feature is entirely about relating itinerary items to dated trip legs — the core domain relationship | PASS |
| II. .NET Application Stack | C# on .NET 10, Blazor UI, Aspire orchestration unchanged. No new runtime or framework | PASS |
| III. Minimal API Vertical Slices | All API work lands in the existing `Features/EmailIngestion` and `Features/TripItems` slices. The new matcher is colocated with the ingestion slice it serves. No MVC, no new top-level project | PASS |
| IV. PostgreSQL with Dapper | One new query file under `Scripts/Queries/EmailIngestion/`, one new idempotent schema script. Dapper throughout; no EF introduced | PASS |
| V. Container App Readiness | No new configuration, no local-only assumptions, no new external dependency. Matching is pure computation over data already reachable | PASS |

**Post-design re-check**: PASS. The Phase 1 design adds no project, no framework, and no infrastructure. The single new database object is a nullable column for traceability, and the single new query reuses the accessible-trips pattern already established in `GetTripsPage.sql`.

## Project Structure

### Documentation (this feature)

```text
specs/024-email-item-leg-matching/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── email-ingestion-placement.md
├── checklists/
│   └── requirements.md  # From /speckit.specify
├── spec.md
└── tasks.md             # Created by /speckit.tasks, not by this command
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   ├── EmailIngestion/
│   │   └── EmailIngestionContracts.cs      # MODIFY: placement fields on ParsedItemDraftDto,
│   │                                       #         nullable TripLegId on confirm response
│   └── TripItems/
│       └── TripItemContracts.cs            # MODIFY: TripLegId becomes Guid? on create/update
│
├── TripPlanner.Api/
│   └── Features/
│       ├── EmailIngestion/
│       │   ├── DraftPlacementMatcher.cs    # NEW: candidate evaluation, no persistence
│       │   ├── GetDraftListEndpoint.cs     # MODIFY: attach placement to each draft
│       │   ├── UpdateDraftEndpoint.cs      # MODIFY: reject a leg that is not on the chosen trip
│       │   ├── ConfirmDraftEndpoint.cs     # MODIFY: validate before create, record the new item id
│       │   └── EmailIngestionMapping.cs    # MODIFY: map placement onto the DTO
│       └── TripItems/
│           └── TrackedItemValidator.cs     # MODIFY: leg optional, window binding when present
│
├── TripPlanner.Database/
│   ├── Scripts/
│   │   ├── Schema/
│   │   │   └── 013_draft_item_traceability.sql        # NEW: parsed_item_drafts.tracked_item_id
│   │   ├── Queries/EmailIngestion/
│   │   │   └── GetPlacementCandidateLegs.sql          # NEW: legs across editable trips
│   │   └── Commands/EmailIngestion/
│   │       └── UpdateParsedItemDraftReviewStatus.sql  # MODIFY: carry tracked_item_id
│   └── EmailIngestion/
│       └── ParsedItemDraftRepository.cs    # MODIFY: candidate-leg query, traceability write
│
└── TripPlanner.Web/
    ├── Features/EmailIngestion/
    │   └── EmailIngestionApiClient.cs      # No change — UpdateDraftAsync already exists
    └── Components/
        ├── EmailIngestion/
        │   └── DraftEditModal.razor        # NEW: trip picker, leg picker, field cleanup
        ├── Pages/EmailIngestion/
        │   └── InboxDrafts.razor           # MODIFY: Edit action, placement hints, host the modal
        └── TripItems/
            └── TrackedItemForm.razor       # MODIFY: allow "No trip leg yet"

tests/
├── TripPlanner.Api.Tests/
│   ├── EmailIngestion/
│   │   ├── DraftPlacementMatcherTests.cs   # NEW: zero/one/many candidates, timezone boundaries
│   │   └── DraftReviewEndpointTests.cs     # MODIFY: confirm now validates; unassigned confirm
│   └── TripItems/
│       └── TrackedItemEndpointTests.cs     # MODIFY: leg-optional and window-refusal coverage
└── TripPlanner.Web.Tests/
    └── EmailIngestion/
        └── InboxReviewPageTests.cs         # MODIFY: edit modal, pickers, unassigned confirm
```

**Structure Decision**: The existing solution layout is unchanged. API work stays inside the two vertical slices it belongs to — `Features/EmailIngestion` owns matching and confirmation, `Features/TripItems` owns the validation rule. Data access stays behind `TripPlanner.Database` with raw SQL, and the Web layer gains one component in the existing `Components/EmailIngestion` area. No new projects.

## Phase 0 — Research

See [research.md](research.md). Seven decisions were resolved, the most consequential being that placement is **computed per request and never persisted as a guess**, which keeps FR-006 satisfiable and removes any possibility of a stale suggestion after a leg is edited.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md) — entities, the one new column, and the placement value objects that exist only in memory
- [contracts/email-ingestion-placement.md](contracts/email-ingestion-placement.md) — request/response shapes and the full status-code matrix for list, update, and confirm
- [quickstart.md](quickstart.md) — runnable validation scenarios covering each user story

## Delivery Slices

Each slice is independently testable, in priority order matching the spec.

| Slice | Story | Delivers | Independently verifiable by |
| ----- | ----- | -------- | --------------------------- |
| 1 | Foundation | Leg becomes optional in contracts and validator; confirm routes through validation | `TrackedItemEndpointTests` — an item saves with no leg, and is refused on an out-of-window leg |
| 2 | US1 (P1) | `DraftPlacementMatcher` plus placement fields on the draft DTO | `DraftPlacementMatcherTests` — a draft inside exactly one leg reports that leg as its sole candidate |
| 3 | US3 (P3) | `DraftEditModal` with trip and leg pickers and field cleanup | `InboxReviewPageTests` — editing a draft's trip repopulates the leg list and persists on save |
| 4 | US2 (P2) | Uncovered-timeframe messaging and confirm-without-a-leg | `InboxReviewPageTests` plus `DraftReviewEndpointTests` — a draft outside every leg confirms to an unassigned item |
| 5 | US4 (P4) | Traceability column and notification parity | `DraftReviewEndpointTests` — a confirmed draft records the item it became |

Slice 1 must land first because slices 2–5 all assume a leg can be absent. Slices 3 and 4 both touch `InboxDrafts.razor` and should be sequenced rather than parallelized.

## Risks

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| `TripLegId` changing from `Guid` to `Guid?` ripples through every call site | Compile-time breakage across API, Web, and tests | The compiler catches all of it. Capture baseline build and test counts before starting, exactly as feature 023 did |
| Relaxing the leg requirement lets manual entry create unassigned items that previously could not exist | Existing timeline, print, and map paths may assume a leg | The timeline already handles `UnassignedItems`; verify the print and map paths during slice 1 rather than discovering it in slice 4 |
| Matching queries every editable trip's legs on each list call | Latency grows with trip count | One indexed query returning tens of rows. Revisit only if it shows up in practice |
| Confirm now validates where it previously did not | Drafts that would have confirmed before may start failing | Intended (FR-020). Surface the specific field error so the traveler can fix it in the modal |

## Complexity Tracking

No constitution violations. Nothing to justify.

