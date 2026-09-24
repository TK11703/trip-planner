# Quickstart: Creating Trip Legs from Forwarded Transportation Bookings

**Feature**: 028-email-transport-leg-ingestion | **Date**: 2026-09-24

Runnable validation for this feature. Each scenario maps to a user story or a named requirement and can be checked independently once its slice lands. See [plan.md](plan.md) for slice ordering, [contracts/email-ingestion-transport-legs.md](contracts/email-ingestion-transport-legs.md) for exact payload shapes, and [data-model.md](data-model.md) for the columns involved.

## Prerequisites

- .NET 10 SDK
- Docker running (Aspire starts PostgreSQL in a container)
- An Azure OpenAI deployment reachable with `DefaultAzureCredential`, as configured in feature 021. Scenarios 1, 2, 5, and 12 exercise recognition; the rest can be driven by seeding drafts directly.
- A relay API key configured for the ingestion endpoint, as set up in feature 022

## Baseline

Capture these **before** changing anything. `ConfirmParsedItemDraftResponse` is a breaking change and will ripple through the API, the Web client, and three test projects; without a baseline a new failure is indistinguishable from a pre-existing one.

```powershell
cd C:\Users\achorpenning\source\repos\trip-planner
dotnet build TripPlanner.slnx
```

Run each suite **separately** — `dotnet test` rejects two project paths in one invocation (MSB1008):

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

Schema scripts in `Scripts/Schema/` are applied in filename order, **exactly once each**, and recorded in the `schema_migrations` ledger with a checksum (feature 026). `016_draft_transport_outcome.sql` applies on the next launch with no manual step. Verify it against a **fresh database**, because that is the only state in which it runs: on an existing database it is applied once and skipped forever after. Never edit it once applied — `DatabaseInitializer` raises `MigrationChecksumMismatchException` on a checksum mismatch and refuses to start.

---

## Scenario 1 — A forwarded flight becomes a flight leg (US1, P1)

**Setup**: Create a trip running 1–10 October with no legs. Forward (or relay-POST) an airline confirmation naming Denver and London Heathrow, a departure of 4 Oct 18:45, an arrival of 5 Oct 11:20, and a booking reference.

**Verify**:

1. `GET /api/email-ingestion/drafts` returns the draft with `proposedOutcome: "leg"`, `transportationMode: "flight"`, and both `origin` and `destination` populated from the email.
2. The review card presents it as a **proposed trip leg**, not a proposed item, and shows the route rather than an item-type badge.
3. Open it. The modal shows mode, origin, destination, start, end, both time zones, title, and confirmation code — not the item fields.
4. Pick the trip. Confirm.
5. `200 OK` with `outcome: "leg"` and `createdTripLegId` set; `trackedItemId` is null.
6. The trip timeline shows a **Flight** travel leg carrying the route, both times, both zones, and the booking reference.
7. **No tracked item was created from that draft** — check the timeline's unassigned lane and the trip's items.
8. The draft is gone from the pending queue.

**Expected**: one confirming action from queue to leg (SC-001), and zero items created from a flight draft without the traveler choosing that (SC-002).

## Scenario 2 — Train, bus, boat, and the non-transport control (US1 §5, §6)

**Setup**: Ingest four emails — a rail ticket, a coach booking, a ferry crossing, and a hotel confirmation.

**Verify**: the first three come back `proposedOutcome: "leg"` with modes `train`, `bus`, and `boat`. The hotel comes back `proposedOutcome: "item"` with null route fields and behaves exactly as it does on `main` (FR-006, SC-009).

## Scenario 3 — The created leg passes hand-entry validation (US1 §7, SC-003)

**Setup**: The leg from Scenario 1.

**Verify**: open it in `TripLegForm` on the trip page and press Save without changing anything. It saves. No validation message appears, and no correction was required.

**Expected**: no email-created leg exists that the traveler could not have typed (SC-010).

## Scenario 4 — The email did not state an arrival (US3, P2)

**Setup**: Ingest a train ticket stating a departure city and time but no arrival time and no arrival zone.

**Verify**:

1. The draft comes back with `endLocal: null` and `endTimeZoneId: null`. **Neither is populated on the traveler's behalf.**
2. The modal names each missing detail specifically — "an end date and time is required", "an end time zone is required" — and lists them together, not one at a time.
3. Confirm is disabled.
4. Force the call anyway (`POST …/confirm` directly). It returns `400` naming `endLocal`, and **nothing was written** — no leg, no item, no change to the trip.
5. Supply both values and confirm. The leg is created with exactly the values supplied.

## Scenario 5 — The labelled default for a missing end zone (FR-034, SC-006)

**Setup**: The draft from Scenario 4, before supplying anything.

**Verify**:

1. Beneath the empty End timezone control sits a suggestion row: *"The email didn't state an arrival time zone. Use the departure zone (`Europe/Paris`)?"* with a **Use this** button.
2. The field itself is **empty**. The missing-detail message is still showing. Confirm is still disabled.
3. Press **Use this**. The field fills, the message clears.
4. Confirm. The leg carries that zone.
5. Repeat without pressing the button, supplying a *different* zone by hand. The hand-supplied zone wins; the suggestion never reasserts itself.
6. Check that **no end date/time suggestion is offered at all** — only the zone (research D5).

**Expected**: zero legs exist carrying an end zone the traveler did not see and accept (SC-006).

## Scenario 6 — Leg to item and back (US2, P1)

**Setup**: The flight draft from Scenario 1, unconfirmed.

**Verify**:

1. Switch the outcome to **Item**. The modal presents item type and leg placement; the route fields disappear from view.
2. A notice names what will not carry: origin, destination, transportation mode, travel cost.
3. The leg dropdown offers only stay and car legs. **The flight leg from Scenario 1 is not offered** (FR-027).
4. Switch back to **Leg**. Origin, destination, mode, and travel cost are all **still there** — nothing was erased (SC-004).
5. Save as Item and confirm. A Reservation item is created, no leg.
6. On a fresh draft, do the reverse: take a recognized hotel, switch to Leg, and verify the modal names each leg detail it is missing and lets you supply them.

## Scenario 7 — The traveler's choices bind (US2 §5, §6, FR-024)

**Verify**: change the recognized mode from Flight to Train and confirm — the leg is a **Train**. Change the trip selection and confirm — the leg lands on the trip chosen, not the one suggested. Leave without confirming — the draft is still pending with every edit intact and the trip unchanged (FR-016).

## Scenario 8 — Car rental becomes a Car leg (FR-035, FR-036)

**Setup**: Ingest a rental confirmation with a pickup counter, a return counter, a reservation number, and a price.

**Verify**:

1. `proposedOutcome: "leg"`, `transportationMode: "car"`.
2. `origin` is the **pickup** location; `destination` is the **return** location.
3. Confirm. A Car travel leg is created — and because Car is the one travel mode that accepts items, the trip page offers **Add item** on it (feature 025 still holds).
4. Ingest the same booking text a second time (vary the message id to defeat dedupe). It classifies as a Car leg again, not an item (FR-036).
5. Override it to an item and confirm. A Reservation item is created instead (FR-037).

## Scenario 9 — Overlap is permitted (FR-021)

**Setup**: A trip with an existing leg covering 4–6 October. Confirm a transport draft whose window is 5–5 October.

**Verify**: it is created. **No refusal and no warning.** Compare against typing the same overlapping leg into `TripLegForm` — identical treatment.

## Scenario 10 — Access withdrawn between recognition and confirm (FR-018)

**Setup**: Have the trip owner drop your access from Collaborator to Viewer while you have a leg draft open.

**Verify**: Confirm returns `404`, an `access.denied` audit row is written, and no leg exists. Repeat with the trip deleted instead of the access downgraded.

## Scenario 11 — Traceability (US4, FR-038 … FR-041)

**Setup**: One draft confirmed as a leg (Scenario 1) and one confirmed as an item (Scenario 6).

```sql
SELECT parsed_item_draft_id, proposed_outcome, review_status,
       tracked_item_id, created_trip_leg_id, inbox_email_id
FROM parsed_item_drafts
WHERE review_status = 'confirmed';
```

**Verify**:

1. The leg row has `proposed_outcome = 'leg'`, `created_trip_leg_id` set, `tracked_item_id` null.
2. The item row has `proposed_outcome = 'item'`, `tracked_item_id` set, `created_trip_leg_id` null — unchanged from feature 024 (FR-041).
3. Join `inbox_email_id` back to `inbox_emails` to reach the originating message from either (FR-039).
4. Delete the created leg. Re-run the query: `review_status` is **still** `confirmed`, `created_trip_leg_id` is now null, and the draft has **not** reappeared in `GET /drafts` (FR-040).
5. Confirm the leg-created notification reached a collaborator, worded "added a new leg to the trip" — the same one a hand-entered leg raises. **No new notification kind appears anywhere** (US4 §5).
6. Confirm an audit row exists with operation `trip-leg.create`, result `success`, against the new leg's id (FR-020).

## Scenario 12 — A draft that predates the feature (US5, FR-045 … FR-047)

**Setup**: On a database seeded **before** the change, leave a pending flight draft in the queue. Apply the change and restart.

**Verify**:

1. The draft is still there, with every recognized detail and every traveler edit intact (FR-042, SC-008).
2. It comes back with `transportRecognitionState: "pending"` and `proposedOutcome: "item"`.
3. `GET /drafts` made **no provider call** — check the Aspire trace. The queue's latency is unchanged.
4. Open the draft. Exactly one `POST …/re-recognize` fires.
5. It returns with `origin`, `destination`, and `transportationMode` filled, `proposedOutcome: "leg"`, and `transportRecognitionState: "current"`.
6. **No new draft appeared in the queue.** The queue count is the same as before (this is the trap: `POST /inbox/{id}/reprocess` inserts drafts; re-recognize must not).
7. Close and reopen the draft. **No second provider call** — the state is no longer `pending`.

## Scenario 13 — Re-recognition preserves traveler edits (FR-046)

**Setup**: A pre-existing pending draft. Before opening it as in Scenario 12, edit and save two things: change the title to something of your own, and **clear** the confirmation code.

**Verify**: after re-recognition, the title is still yours and the confirmation code is still **empty**. Origin, destination, and mode — which you never touched — are filled. The `traveler_edited_fields` array on the row lists `title` and `confirmationCode`.

## Scenario 14 — Re-recognition fails (FR-047)

**Setup**: Point the Azure OpenAI configuration at an unreachable deployment. Open a `pending` draft.

**Verify**: the call returns `200`, the draft is unchanged except `transportRecognitionState: "unavailable"`, no error banner blocks the queue, and the draft **still confirms cleanly as an item**. Reopen it — no retry storm; the state stays `unavailable`.

## Scenario 15 — Nothing already confirmed is touched (FR-043, FR-044, FR-048)

**Setup**: A database containing reservation items previously confirmed from flight emails, at least one of them unassigned.

**Verify**:

1. After the change, every one of those items exists, unmoved, with the same leg (or none), the same dates, and the same id.
2. The unassigned ones still render in the timeline's **Unassigned / Needs a trip leg** lane and can still be related to a leg by hand (FR-044).
3. **No "convert to leg" action appears anywhere** — not on the item, not on the timeline, not in the queue (FR-048).
4. `016_draft_transport_outcome.sql` contains no `INSERT`, `UPDATE`, or `DELETE` against `tracked_items` or `trip_legs`. The only permitted mention of either table is the `created_trip_leg_id` foreign-key clause referencing `trip_legs (trip_leg_id)`, which FR-040 depends on; `tracked_items` must not appear at all. Read the script and confirm.

## Scenario 16 — Redaction and limits still apply to transport (FR-007)

**Verify**: ingest a transport email whose body contains a long digit run in the route text. The stored `origin`/`destination` carry `[redacted]`. A low-confidence transport item below 0.5 produces no draft. An email describing more than 20 bookings produces at most 20.

## Scenario 17 — Ingestion plumbing is unchanged (SC-009)

**Verify**: `RelayIngestionEndpointTests`, `RelayIngestionDeduplicationTests`, and `RelayIngestionAuthorizationTests` pass **without modification**. If any needed changing, the blast radius exceeded the plan.

Also confirm `NoMailboxMonitoringTests` still passes — re-recognition runs inside a request, and no background service was introduced.

---

## Exit criteria

- [X] `dotnet build TripPlanner.slnx` — 0 errors, warning count at or below baseline *(9 warnings, exactly baseline; the one new CS8604 was fixed rather than accepted)*
- [X] All three test suites at or above baseline pass counts *(Api 295/234, Web 323/305, Database 79/59)*
- [X] `016_draft_transport_outcome.sql` applies cleanly on a fresh database, is recorded once in `schema_migrations`, and is skipped on the second start *(verified across five application starts; also applied standalone to a throwaway PostgreSQL 16 container)*
- [X] Scenarios 1–17 verified, with 1, 4, 6, 8, 11, 12, 13, 14 covered by automated tests *(Scenarios 12 and 13 were additionally walked live once `AzureOpenAI:Endpoint` was configured: a legacy item-draft was promoted to a Flight leg carrying OGG → HNL and 128.00 USD, all extracted from the email body, with the end and its zone correctly left for the traveler)*
- [X] No tracked item is created from a draft confirmed as a leg, and no leg from one confirmed as an item (SC-002) *(asserted in `ConfirmDraftAsLegTests`; confirmed by hand for flight, train, car, and both overrides)*
- [X] Every leg created from email passes `TripLegForm` validation unchanged (SC-003)
- [X] No leg exists carrying an end date/time, end time zone, or origin the traveler did not see and accept (SC-006) *(the end-zone suggestion is inert until pressed; no end date/time default is offered anywhere; no server path computes either)*
- [X] Every confirmed draft records both **which kind** of entity it became and **which** one (SC-007)
- [X] Zero already-confirmed items were altered or deleted (SC-008) *(`016` issues no DML against either timeline table — asserted by `TheMigrationWritesToNeitherTimelineTable`; no convert action exists anywhere in the UI)*
- [X] `TripLegValidator.cs` shows **no diff** — the same validation guards both paths by construction (SC-003, SC-010) *(also verified for `TripLegForm.razor`, `GetPlacementCandidateLegs.sql`, and `FillMissingTimeZonesAsync`)*

### Defects found during validation

Six, none of which the unit suites caught at the time:

1. **Dapper could not materialize `traveler_edited_fields`** through a positional record, so `GET /drafts` returned 500 for every traveler. Fixed by property-based mapping; now guarded by `ADraftReadsBackItsTravelerEditedFields` against real PostgreSQL.
2. **The review queue silently demoted a leg to an item.** `ConfirmAsync` writes the chosen placement immediately before confirming, and the request was built without the transport fields, so `ProposedOutcome` defaulted to `Item` — reproducing the original complaint. Guarded by `ConfirmingALegDraftFromTheQueueNeverRewritesItToAnItem`.
3. **The designed backfill would have mislabelled new drafts.** A guarded `UPDATE` keyed on `review_status` also matches drafts created after the migration. Replaced with the column-default technique, which confines the effect to rows that predate the column.
4. **A recognizer that cannot be constructed left drafts stuck `pending`.** `AzureOpenAI:Endpoint` being unset made DI resolution throw before any handler code ran, so FR-047 never settled the state and every open retried the same failure. The recognizer is now resolved inside the try block via a factory; guarded by `ARecognizerThatCannotBeConstructedStillSettlesTheState`.
5. **A failing re-recognition tore down the Blazor circuit.** `OnInitializedAsync` caught only `HttpRequestException` and `TaskCanceledException`, so anything else escaped and left an empty form on screen — strictly worse than an un-enriched draft. Both the modal and the API client now treat any failure as "no refresh"; guarded by `AThrowingReRecognitionStillOpensTheDraftIntact`.
6. **`DefaultAzureCredential` selected the wrong developer identity.** On a developer machine it reaches `VisualStudioCredential` before `AzureCliCredential`, and Visual Studio is commonly signed in as a different account than `az login`. That credential returns a valid token which the data plane rejects with 401 — an outage indistinguishable from a configuration error. The AppHost now pins the chain for local runs via `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` (Azure.Identity 1.19.0; individual credential names need 1.15.0+), keeping the choice in orchestration rather than product code. Hosted environments never run the AppHost and still use managed identity.
