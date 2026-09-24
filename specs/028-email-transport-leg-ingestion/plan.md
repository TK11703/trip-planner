# Implementation Plan: Creating Trip Legs from Forwarded Transportation Bookings

**Branch**: `028-email-transport-leg-ingestion` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/028-email-transport-leg-ingestion/spec.md`

## Summary

Ingestion can create exactly one thing: a tracked item. Feature 025 then made it impossible for that item to be attached to the flight, train, bus, or boat leg it describes. A forwarded flight confirmation therefore produces an entity the itinerary model has no valid place for — the contradiction this feature exists to remove.

Everything needed to fix it already exists apart from the middle. The leg model carries origin, destination, mode, travel cost, and a confirmation code. `TripLegValidator` already validates exactly that shape. `AuditOperations.TripLegCreate` and `ItineraryChangeKind.TripLegCreated` already exist and are already raised by hand entry. What is missing is that recognition has no notion of a journey, and the review queue has no second write.

Five moves close it:

1. **Teach recognition to report a route.** The system prompt's per-item schema gains `transportationMode`, `origin`, `destination`, `travelCost`, and `travelCostCurrency`, and `itemType` gains `train`, `bus`, and `boat`. The model reports facts; a new `DraftOutcomeClassifier` and `TransportationModeInterpreter` decide leg-versus-item **in code**, deterministically, so FR-036's consistency requirement is provable rather than hoped for.
2. **Record the outcome on the draft.** `parsed_item_drafts` gains `proposed_outcome`, the route fields, and a `created_trip_leg_id` for traceability. The outcome is proposed by recognition, overridable by the traveler through the existing `PUT /drafts/{id}`, and binding at confirmation.
3. **Give the queue a second write.** `POST /drafts/{id}/confirm` keeps its single route and branches on the stored outcome. The new leg branch names each missing detail a leg requires, then hands a `CreateTripLegRequest` to the **same** `TripLegValidator`, the same `CreateLegAsync`, the same audit operation, and the same notification hand entry uses. `TripLegValidator` is not modified — that is what makes SC-003 true by construction rather than by testing.
4. **Make the review screen present the chosen outcome.** `DraftEditModal` gains a leg/item toggle, the transport fields laid out as `TripLegForm` lays them out, a notice naming what will not carry across a switch, and a single labelled one-click default for a missing end time zone — shown, never applied.
5. **Catch up the drafts already in the queue.** A new `POST /drafts/{id}/re-recognize`, called once when the traveler first opens a pre-existing draft, merges transport details into the existing row without overwriting anything they supplied by hand. It is a traveler-triggered write, not a side effect of the list read, so the queue costs nothing extra.

Nothing already confirmed is touched. The migration contains no `INSERT`, `UPDATE`, or `DELETE` against `tracked_items` or `trip_legs` — its only mention of either is the `created_trip_leg_id` foreign key.

## Technical Context

**Language/Version**: C# 14 on .NET 10; SQL for PostgreSQL

**Primary Dependencies**: ASP.NET Core Minimal APIs, Blazor interactive server, Dapper, Npgsql, .NET Aspire, Azure OpenAI (`Azure.AI.OpenAI` with `DefaultAzureCredential`), xUnit, bUnit. No new packages

**Storage**: PostgreSQL. Raw SQL in `TripPlanner.Database`; schema scripts applied exactly once by `DatabaseInitializer` and recorded in the `schema_migrations` ledger with a checksum (feature 026), so `016_draft_transport_outcome.sql` takes the next number after `015` and must be correct before it is first applied — editing an applied migration blocks startup

**Testing**: xUnit across `TripPlanner.Api.Tests` and `TripPlanner.Database.Tests`; bUnit in `TripPlanner.Web.Tests`; Playwright in `TripPlanner.E2E.Tests`

**Target Platform**: Linux containers, Azure Container Apps; browser-hosted Blazor

**Project Type**: Web — Blazor front end, Minimal API middle tier, shared contracts, PostgreSQL data project

**Performance Goals**:

- `GET /drafts` gains **zero** provider calls and zero extra round trips. Feature 024's SC-008 (placement computed inside the existing request) is preserved exactly
- Re-recognition costs at most **one** Azure OpenAI call per pre-existing draft for that draft's entire life, initiated by the traveler opening it
- Confirm-as-leg is one trip read plus one insert, matching `TripLegEndpoints.CreateAsync`

**Constraints**:

- No background services in the API — feature 022 established this and `NoMailboxMonitoringTests` guards it. Re-recognition runs inside the request
- Schema scripts are desired-state and unversioned; every new script must be safe to re-run
- The leg model, the transportation-mode set, and the feature-025 eligibility triggers are out of scope and must not change
- `TripLegValidator` must not change, so hand entry carries no risk from this feature
- Breaking contract changes are acceptable; no external consumers exist (confirmed for feature 024 and unchanged)
- No automatic conversion, movement, or deletion of already-confirmed items (FR-043, FR-048)

**Scale/Scope**: Tens of pending drafts per traveler. Nine new columns on one table, one new endpoint, two new API types, one new Blazor component, and additive edits to five existing files per layer

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
| --------- | ---------- | ------- |
| I. Trip Planning Domain | The feature makes a forwarded journey become a dated trip leg rather than an entity the itinerary cannot hold. It is the core domain relationship being repaired | PASS |
| II. .NET Application Stack | C# on .NET 10, Blazor UI, Aspire orchestration unchanged. No new runtime, framework, or package | PASS |
| III. Minimal API Vertical Slices | Every API change lands in the existing `Features/EmailIngestion` slice. `Features/TripItems` is **read and reused, not modified** — the validator, repository, audit operation, and notification kind are all consumed as they stand. No MVC, no new project | PASS |
| IV. PostgreSQL with Dapper | One new idempotent schema script, one new command file, five modified SQL files, all under `TripPlanner.Database`. Dapper throughout; no EF introduced | PASS |
| V. Container App Readiness | No new configuration, no new external dependency, no local-only assumption. The one added provider call is on an existing, already-configured client, inside a request | PASS |

**Pre-Research Result**: PASS. No constitution violation requires justification.

**Post-Design Re-check**: PASS. The Phase 1 design adds no project, package, or infrastructure component. The one judgement worth recording is `travel_cost_currency` — a column with a single display-only consumer, justified in [research.md](research.md) D9 against the constitution's prohibition on unused infrastructure: it is what lets the traveler notice a currency mismatch during review, which FR-010 requires them to do.

## Project Structure

### Documentation (this feature)

```text
specs/028-email-transport-leg-ingestion/
├── plan.md              # This file
├── research.md          # Phase 0 output — ten decisions
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output — 17 validation scenarios
├── contracts/           # Phase 1 output
│   └── email-ingestion-transport-legs.md
├── checklists/
│   └── requirements.md  # From /speckit.specify
├── spec.md
└── tasks.md             # Created by /speckit.tasks, not by this command
```

### Source Code (repository root)

```text
src/
├── TripPlanner.Contracts/
│   └── EmailIngestion/
│       └── EmailIngestionContracts.cs          # MODIFY: DraftOutcome, DraftRecognitionState,
│                                               #         route fields on the DTO and update request,
│                                               #         reshaped ConfirmParsedItemDraftResponse
│
├── TripPlanner.Api/Features/
│   ├── EmailIngestion/
│   │   ├── TransportationModeInterpreter.cs    # NEW: recognizer vocabulary -> TransportationModes, or null
│   │   ├── DraftOutcomeClassifier.cs           # NEW: the one deterministic leg-vs-item rule
│   │   ├── EmailTextAssembler.cs               # NEW: AssembleText moved out of RelayMessageProcessor
│   │   ├── ConfirmDraftAsLeg.cs                # NEW: gap checks, TripLegValidator, CreateLegAsync, audit, notify
│   │   ├── LegConfirmationGaps.cs              # NEW: the named-missing-detail result
│   │   ├── ReRecognizeDraftEndpoint.cs         # NEW: POST /drafts/{id}/re-recognize
│   │   ├── DraftReRecognitionService.cs        # NEW: recognize, select the matching item, merge
│   │   ├── EmailParserService.cs               # MODIFY: prompt schema, RecognizedItem, ToDraft.
│   │   │                                       #         FillMissingTimeZonesAsync deliberately untouched
│   │   ├── ConfirmDraftEndpoint.cs             # MODIFY: branch on stored outcome; item path unchanged
│   │   │                                       #         except train/bus/boat in NormalizeItemType
│   │   ├── UpdateDraftEndpoint.cs              # MODIFY: outcome + route fields, traveler-edited diff
│   │   ├── EmailIngestionMapping.cs            # MODIFY: map the new columns onto the DTO
│   │   ├── RelayMessageProcessor.cs            # MODIFY: delegate to EmailTextAssembler. Otherwise unchanged
│   │   ├── EmailIngestionEndpointRouteBuilderExtensions.cs  # MODIFY: map the new route
│   │   └── DraftPlacementMatcher.cs            # UNCHANGED
│   └── TripItems/
│       ├── TripLegValidator.cs                 # UNCHANGED — deliberately. See research D4
│       ├── TripLegEndpoints.cs                 # UNCHANGED
│       └── TrackedItemValidator.cs             # UNCHANGED
│
├── TripPlanner.Database/
│   ├── Scripts/
│   │   ├── Schema/
│   │   │   └── 016_draft_transport_outcome.sql             # NEW: nine columns, four checks, one guarded backfill
│   │   ├── Commands/EmailIngestion/
│   │   │   ├── InsertParsedItemDraft.sql                   # MODIFY
│   │   │   ├── UpdateParsedItemDraft.sql                   # MODIFY: fields + traveler_edited_fields
│   │   │   ├── UpdateParsedItemDraftReviewStatus.sql       # MODIFY: created_trip_leg_id alongside tracked_item_id
│   │   │   └── MergeParsedItemDraftRecognition.sql         # NEW: the FR-046 merge rule, in SQL
│   │   └── Queries/EmailIngestion/
│   │       ├── GetParsedItemDrafts.sql                     # MODIFY: project the new columns
│   │       ├── GetParsedItemDraftById.sql                  # MODIFY: project the new columns
│   │       └── GetPlacementCandidateLegs.sql               # UNCHANGED — already excludes restricted legs
│   └── EmailIngestion/
│       ├── IParsedItemDraftRepository.cs       # MODIFY: records gain fields; MergeRecognitionAsync added
│       └── ParsedItemDraftRepository.cs        # MODIFY
│
└── TripPlanner.Web/
    ├── Features/EmailIngestion/
    │   └── EmailIngestionApiClient.cs          # MODIFY: ReRecognizeDraftAsync; reshaped confirm response
    └── Components/
        ├── EmailIngestion/
        │   ├── DraftEditModal.razor            # MODIFY: outcome toggle, leg fields, carry-across notice,
        │   │                                   #         leg-mode validation model
        │   └── DraftSuggestionRow.razor        # NEW: the labelled one-click default (FR-034)
        ├── Pages/EmailIngestion/
        │   └── InboxDrafts.razor               # MODIFY: outcome badge, suppress placement for leg drafts,
        │                                       #         trigger re-recognition, leg-aware CanConfirm
        └── TripItems/
            └── TripLegForm.razor               # UNCHANGED — referenced for layout conventions only

tests/
├── TripPlanner.Api.Tests/EmailIngestion/
│   ├── TransportRecognitionTests.cs            # NEW: canned payloads -> route, mode, outcome
│   ├── DraftOutcomeClassifierTests.cs          # NEW: synonyms, unsupported modes, car_rental
│   ├── ConfirmDraftAsLegTests.cs               # NEW: creation, every gap, nothing written on refusal
│   ├── DraftReRecognitionTests.cs              # NEW: merge, edit preservation, failure tolerance, no new drafts
│   ├── DraftReviewEndpointTests.cs             # MODIFY: reshaped confirm response
│   └── ConfirmDraftItemTypeTests.cs            # MODIFY: train/bus/boat -> Reservation
├── TripPlanner.Database.Tests/EmailIngestion/
│   └── ParsedItemDraftTransportTests.cs        # NEW: round trip, checks, script idempotency
└── TripPlanner.Web.Tests/EmailIngestion/
    ├── DraftLegOutcomeTests.cs                 # NEW: toggle, leg fields, gaps, suggestion row
    └── InboxReviewPageTests.cs                 # MODIFY: leg drafts in the queue
```

**Structure Decision**: The existing solution layout is unchanged and no project is added. All API work lands inside the `Features/EmailIngestion` vertical slice, which is where both the problem and every new type belong. `Features/TripItems` is consumed but not modified — the validator, the repository, the audit operation, and the notification kind are reused exactly as hand entry uses them, which is the mechanism behind SC-003 and SC-010 rather than a stylistic preference. Data access stays behind `TripPlanner.Database` with raw SQL, and the Web layer gains one small component in the existing `Components/EmailIngestion` area.

## Phase 0 — Research

See [research.md](research.md). Ten decisions were resolved. The four that shape everything else:

- **D1** — recognition reports facts (`transportationMode`, `origin`, `destination`); a code classifier decides leg-versus-item, so FR-036's consistency is deterministic and testable without a provider.
- **D2** — nothing but the traveler ever fills an empty end time zone. The tempting Azure Maps lookup from `destination` is rejected because it would fill a value the email did not state, which is precisely what FR-034 prohibits and what SC-006 counts as a defect.
- **D4** — `TripLegValidator` cannot represent an absent end (its parameters are non-nullable), so the endpoint names missing details and the validator is left untouched. The *complete* list of gaps is produced by the review screen's validation model; the API's single-field refusal is the backstop.
- **D6** — re-recognition is a traveler-triggered write on a new route, gated by a three-state column, not a side effect of `GET /drafts`. It must not reuse `RelayMessageProcessor.ReprocessAsync`, which inserts new drafts and would duplicate the queue.

## Phase 1 — Design & Contracts

- [data-model.md](data-model.md) — the nine new columns, the four checks, the one guarded backfill, the SQL merge rule, and the contract changes
- [contracts/email-ingestion-transport-legs.md](contracts/email-ingestion-transport-legs.md) — request/response shapes and the full status-code matrix for list, update, re-recognize, and both confirm branches
- [quickstart.md](quickstart.md) — seventeen runnable scenarios covering each user story, each clarified decision, and each of the three "must not happen" guarantees

## Delivery Slices

Each slice is independently testable, in priority order matching the spec.

| Slice | Story | Delivers | Independently verifiable by |
| ----- | ----- | -------- | --------------------------- |
| 1 | Foundation | `016` schema script, contract types, repository and SQL carrying the new columns | `ParsedItemDraftTransportTests` — a draft round-trips outcome, route, mode, and cost, and the script is a no-op on a second run |
| 2 | US1 (P1) | Prompt schema, `TransportationModeInterpreter`, `DraftOutcomeClassifier`, redaction over the new fields | `TransportRecognitionTests` — a flight payload yields outcome Leg with both endpoints; a hotel payload is byte-identical to today |
| 3 | US1 (P1) | `ConfirmDraftAsLeg`: creation, audit, the existing leg notification, `created_trip_leg_id` | `ConfirmDraftAsLegTests` — a complete transport draft creates a Travel leg and **no** tracked item |
| 4 | US3 (P2) | The gap checks for end, end zone, origin, destination, and mode | `ConfirmDraftAsLegTests` — each gap returns `400` naming its own field and writes nothing |
| 5 | US2 (P1) | Outcome toggle, transport fields in the modal, the carry-across notice, leg-mode validation model | `DraftLegOutcomeTests` — switching presents the right fields, names what will not carry, and loses nothing on the round trip |
| 6 | US3 (P2) | `DraftSuggestionRow` — the labelled one-click end-zone default | `DraftLegOutcomeTests` — the field stays empty and Confirm stays disabled until the button is pressed |
| 7 | US4 (P2) | Traceability read paths and the deleted-leg case | `ConfirmDraftAsLegTests` — a confirmed draft records both kind and id; deleting the leg leaves it confirmed and out of the queue |
| 8 | US5 (P3) | `re-recognize`, `transport_recognition_state`, `traveler_edited_fields`, failure tolerance | `DraftReRecognitionTests` — merges, preserves edits and cleared fields, tolerates provider failure, creates no new draft |

Slice 1 must land first: slices 2–8 all assume the columns exist. Slices 5 and 6 both touch `DraftEditModal.razor` and should be sequenced rather than parallelized, exactly as feature 024 sequenced its two `InboxDrafts.razor` slices. Slice 3 depends on slice 2 only for realistic test data and can be built against hand-seeded drafts if recognition work runs long.

## Risks

| Risk | Impact | Mitigation |
| ---- | ------ | ---------- |
| Reusing `RelayMessageProcessor.ReprocessAsync` for FR-045 | Every re-opened legacy draft silently doubles in the queue — a data bug the traveler sees, not a test failure | `DraftReRecognitionService` is a separate type that only ever `UPDATE`s. `DraftReRecognitionTests` asserts the pending-draft count is unchanged after re-recognition. Quickstart Scenario 12 §6 checks it by hand |
| The Azure Maps end-zone shortcut gets added during implementation | Legs created with an arrival zone the traveler never saw — a direct SC-006 defect, and invisible unless looked for | Recorded as a rejected alternative in research D2 specifically so it is refused on review. `FillMissingTimeZonesAsync` must show no diff |
| `ConfirmParsedItemDraftResponse` reshaping ripples through API, Web, and three test projects | Compile-time breakage across the solution | The compiler catches all of it. Capture baseline build and test counts first, exactly as features 023 and 024 did |
| The end date/time is often absent, so US3's path is the common case rather than the exception | Travelers hit a refusal on a large share of transport drafts and may perceive the feature as broken | Anticipated by the spec ("This is not an edge case"). Mitigated by naming every gap at once in the modal, disabling Confirm rather than letting it fail, and offering the item outcome as a working alternative (FR-033) |
| `travel_cost_currency` is a column with one display-only consumer | Looks like unused infrastructure on review | Justified in research D9 and in the post-design constitution re-check. If the review label is ever dropped, the column goes with it |
| Nine columns on `parsed_item_drafts` in one script | `016` is applied exactly once and cannot be corrected in place — an edit after it has been applied raises `MigrationChecksumMismatchException` and blocks startup | Get it right before the first apply. Follow `014_trip_leg_modes.sql` for shape, set legacy state through the column default rather than a backfill `UPDATE`, and verify against a fresh database. Any later correction ships as `017` |
| A reader of the spec's Context section proposes backfilling existing flight reservations into legs | Violates FR-043 and FR-048, and destroys traveler data | The clarification session ruled it out explicitly. Research D10 states it, the data model states it, and quickstart Scenario 15 §4 checks the script contains no `INSERT`/`UPDATE`/`DELETE` against `tracked_items` or `trip_legs` |

## Complexity Tracking

No constitution violations. Nothing to justify.
