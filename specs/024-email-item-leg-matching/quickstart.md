# Quickstart: Matching Ingested Email Items to Trip Legs

**Feature**: 024-email-item-leg-matching | **Date**: 2026-08-05

Runnable validation for this feature. Each scenario maps to a user story and can be checked independently once its slice lands. See [plan.md](plan.md) for slice ordering and [contracts/email-ingestion-placement.md](contracts/email-ingestion-placement.md) for exact payload shapes.

## Prerequisites

- .NET 10 SDK
- Docker running (Aspire starts PostgreSQL in a container)
- A relay API key configured for the ingestion endpoint, as set up in feature 022

## Baseline

Capture these **before** changing anything. Feature 023 proved their value — the `Guid` → `Guid?` change will ripple widely, and without a baseline it is impossible to tell a new failure from a pre-existing one.

```powershell
cd C:\Users\achorpenning\source\repos\trip-planner
dotnet build TripPlanner.slnx
```

Then run each suite **separately** — `dotnet test` rejects two project paths in one invocation (MSB1008):

```powershell
dotnet test tests/TripPlanner.Api.Tests/TripPlanner.Api.Tests.csproj
dotnet test tests/TripPlanner.Web.Tests/TripPlanner.Web.Tests.csproj
dotnet test tests/TripPlanner.Database.Tests/TripPlanner.Database.Tests.csproj
```

Record error count, warning count, and passed/skipped per suite. Warnings are expected to stay flat; CA1707 noise in test projects is accepted repo-wide.

## Run the app

```powershell
dotnet run --project src/TripPlanner.AppHost/TripPlanner.AppHost.csproj
```

Or use the **watch (Aspire hot reload)** task for C# hot reload across AppHost, API, and Web.

The schema is desired-state: `DatabaseInitializer` replays every script in `Scripts/Schema/` on start, ordered by filename. `013_draft_item_traceability.sql` applies on the next launch with no manual step — confirm it is idempotent by starting twice.

---

## Scenario 1 — A draft lands on the right leg (US1, P1)

**Setup**: Create a trip with one leg covering 12–15 August. Forward or POST an email describing a hotel stay starting 13 August.

**Verify**:

1. `GET /api/email-ingestion/drafts` returns the draft with `placement.status == "matched"` and both `suggestedTripId` and `suggestedTripLegId` populated.
2. The inbox page shows the draft with that trip and leg already selected, and the reason for the match visible.
3. Confirm. An item appears on that leg in the trip timeline carrying the recognized type, title, location, dates, zones, and confirmation code.
4. The draft is gone from the pending queue.

**Expected**: One deliberate action from opening the queue to a placed item (SC-001).

## Scenario 2 — Two legs qualify (US1, ambiguity)

**Setup**: Add a second overlapping leg covering the same dates, or a second trip that overlaps.

**Verify**: `placement.status == "ambiguous"`, both candidates returned, **neither pre-selected**, and Confirm stays disabled until the traveler chooses (FR-005).

## Scenario 3 — No leg covers the timeframe (US2, P2)

**Setup**: Create a trip spanning 10–20 August whose only leg covers 12–15 August. Ingest a car rental for 17 August.

**Verify**:

1. `placement.status == "noLegCovers"` — *not* `outsideTripDates`, because 17 August is inside the trip's own range.
2. The inbox states that no leg covers those dates and names them.
3. Assign the draft to the trip, leave the leg empty, confirm.
4. `200 OK` with `tripLegId: null`.
5. The trip timeline shows it in the **Unassigned / Needs a trip leg** lane with its dates.
6. Create a leg covering 16–18 August, edit the item, relate it to that leg. It moves out of the unassigned lane.

**Expected**: Resolution without leaving the review queue (SC-004), and the unassigned item stays visible until placed (SC-005).

## Scenario 4 — Outside the trip's dates entirely (US2, edge)

**Setup**: Ingest a booking for 5 September against a trip running 10–20 August.

**Verify**: `placement.status == "outsideTripDates"` and the message differs from Scenario 3 (FR-010).

## Scenario 5 — Trip with no legs at all (US2, edge)

**Setup**: Create a trip with zero legs. Ingest any booking and assign it to that trip.

**Verify**: Confirm succeeds and produces an unassigned item. No "Add a trip leg before adding an item" error — that rejection is gone (FR-014).

## Scenario 6 — Editing a draft before saving (US3, P3)

**Setup**: Any pending draft with imperfect parsed data.

**Verify**:

1. Open the edit modal from the inbox.
2. Change the trip. The leg list repopulates from the new trip and any prior leg choice clears (FR-018).
3. Correct the title, location, dates, time zones, confirmation code, and notes.
4. Save. `PUT` returns `200` with a **recomputed** `placement` reflecting the new dates (FR-008).
5. Reopen the draft — the edits persisted.
6. Pick a leg belonging to a *different* trip via a crafted request. Expect `400` naming `tripLegId`.

## Scenario 7 — Out-of-window assignment is refused (US4, P4)

**Setup**: A draft dated 17 August assigned to a leg covering 12–15 August.

**Verify**:

1. Confirm returns `400` naming `startLocal`, with the same wording the item form produces.
2. The same pairing typed into the item form on the trip page is refused identically.
3. No item was created — check the timeline.

**Expected**: Zero items exist outside their assigned leg's window (SC-006). This is the regression that motivated the feature: before this change, confirm bypassed the validator entirely and would have written the item.

## Scenario 8 — Time zones decide containment (edge)

**Setup**: A leg covering 12–15 August in `America/Los_Angeles`. A flight landing 16 August 01:00 in `Asia/Tokyo` — which is 15 August 09:00 Pacific, therefore **inside** the window.

**Verify**: The draft matches that leg. A naive wall-clock comparison would reject it (FR-002).

## Scenario 9 — Viewer trips are excluded (FR-003)

**Setup**: Have another traveler share a trip with you at **Viewer** level, with a leg covering the draft's dates.

**Verify**: That trip appears in neither `placement.candidates` nor the modal's trip picker.

## Scenario 10 — Traceability (FR-025)

**Setup**: Confirm any draft.

**Verify**: The `parsed_item_drafts` row has `review_status = 'confirmed'` and `tracked_item_id` set to the created item.

```sql
SELECT parsed_item_draft_id, review_status, tracked_item_id
FROM parsed_item_drafts
WHERE review_status = 'confirmed';
```

## Scenario 11 — Ingestion is unchanged (SC-009)

**Verify**: Recognition, duplicate suppression, and sender-to-traveler matching behave exactly as before. `RelayIngestionEndpointTests`, `RelayIngestionDeduplicationTests`, and `RelayIngestionAuthorizationTests` pass without modification — if any of them needed changing, the blast radius exceeded the plan.

Also confirm `NoMailboxMonitoringTests` still passes: matching runs inside a request, never in a background service.

---

## Exit criteria

- [X] `dotnet build TripPlanner.slnx` — 0 errors, warning count at or below baseline
      *(0 errors, 35 warnings on an incremental build; baseline 0 errors / 261 warnings on a clean build)*
- [X] All three test suites at or above baseline pass counts
      *(Api 128/6/134 vs 105/6/111; Web 145/3/148 vs 128/3/131; Database 5/18/23 vs 2/17/19)*
- [ ] Scenarios 1–11 verified manually against a running app
      *(Scenarios 1, 2, 8, 9 covered by automated tests; 3–7, 10, 11 still need a manual pass — see T039, T050, T062)*
- [ ] `013_draft_item_traceability.sql` applies cleanly on a fresh database **and** on a second start
      *(idempotency asserted by `ParsedItemDraftTraceabilityTests`; the two-start check is T063 and needs a live database)*
- [X] No item exists whose dates fall outside its assigned leg's window
      *(`ConfirmDraftEndpoint` runs `TrackedItemValidator` before writing; asserted by `ConfirmingOntoALegThatDoesNotContainTheItemIsRefusedAndWritesNothing`)*
- [X] Every confirmed draft records the item it became
      *(asserted by `ASuccessfulConfirmRecordsTheItemAndRaisesTheSameNotificationAManualAddRaises`)*
