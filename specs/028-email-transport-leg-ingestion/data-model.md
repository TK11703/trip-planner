# Data Model: Creating Trip Legs from Forwarded Transportation Bookings

**Feature**: 028-email-transport-leg-ingestion | **Date**: 2026-09-24

Every schema change lands on one table: `parsed_item_drafts`. `trip_legs`, `tracked_items`, the feature-025 eligibility triggers, and the leg contracts are read and reused, never altered. There is no data migration of existing itinerary content — see [research.md](research.md) D10.

## Persisted entities

### `parsed_item_drafts` (MODIFIED)

The review-queue row. All existing columns keep their meaning exactly.

| Column | Type | Notes |
| ------ | ---- | ----- |
| `parsed_item_draft_id` | `uuid` PK | |
| `inbox_email_id` | `uuid` NOT NULL | FK → `inbox_emails`, `ON DELETE CASCADE`. The trace back to the message (FR-039) |
| `user_id` | `text` NOT NULL | Owning traveler |
| `trip_id` | `uuid` NULL | FK → `trips`, `ON DELETE SET NULL`. The traveler's explicit choice of trip. Required by **both** outcomes |
| `trip_leg_id` | `uuid` NULL | The leg an **item** will be placed on. Meaningless for the leg outcome; retained across a switch, never cleared (D8) |
| `item_type` | `text` NULL | Recognizer vocabulary, now also `train`, `bus`, `boat`. Meaningless for the leg outcome; retained |
| `title`, `location` | `text` NULL | Shared by both outcomes (FR-025) |
| `start_local`, `end_local` | `timestamp` NULL | Local wall time, no offset. Shared |
| `start_timezone_id`, `end_timezone_id` | `text` NULL | IANA ids. Shared. `end_timezone_id` is optional for an item and **mandatory** for a leg (FR-029) |
| `confirmation_code`, `notes` | `text` NULL | Shared |
| `confidence` | `double precision` NOT NULL | Recognition confidence. Unchanged, and still subject to the 0.5 threshold (FR-007) |
| `review_status` | `text` NOT NULL | `pending_review` \| `confirmed` \| `discarded`. Unchanged |
| `created_at_utc` | `timestamptz` NOT NULL | |
| `tracked_item_id` | `uuid` NULL | FK → `tracked_items`, `ON DELETE SET NULL`. Feature 024. Unchanged (FR-041) |
| **`proposed_outcome`** | **`text` NOT NULL DEFAULT `'item'`** | **NEW.** `'leg'` \| `'item'`. Proposed by recognition, decided by the traveler, binding at confirm (FR-008, FR-024) |
| **`origin`** | **`text` NULL** | **NEW.** Where the journey starts. Pickup counter for a car rental (FR-003, FR-035) |
| **`destination`** | **`text` NULL** | **NEW.** Where it arrives. Return counter for a car rental (FR-003, FR-035) |
| **`transportation_mode`** | **`text` NULL** | **NEW.** One of `flight`\|`train`\|`bus`\|`boat`\|`car`, or null (FR-002) |
| **`travel_cost`** | **`numeric(12,2)` NULL** | **NEW.** The numeric amount only. Matches `trip_legs.travel_cost` exactly (FR-010) |
| **`travel_cost_currency`** | **`text` NULL** | **NEW.** Display-only label for the review screen. Never written to a leg (D9) |
| **`created_trip_leg_id`** | **`uuid` NULL** | **NEW.** FK → `trip_legs`, `ON DELETE SET NULL`. The leg this draft became (FR-038) |
| **`transport_recognition_state`** | **`text` NOT NULL DEFAULT `'current'`** | **NEW.** `current` \| `pending` \| `unavailable`. Drives FR-045 (D6) |
| **`traveler_edited_fields`** | **`text[]` NOT NULL DEFAULT `'{}'`** | **NEW.** Field names the traveler has saved by hand. Protects them from re-recognition (FR-046, D7) |

**New script**: `Scripts/Schema/016_draft_transport_outcome.sql` — the next number after `015`. Written defensively with `ADD COLUMN IF NOT EXISTS` / `DROP CONSTRAINT IF EXISTS` to match the house style of `014`, though since feature 026 those guards are belt-and-braces rather than load-bearing: `DatabaseInitializer` applies each script exactly once and records it in the `schema_migrations` ledger with a checksum. The operative constraint is the opposite one — an applied migration must never be edited, because a checksum mismatch raises `MigrationChecksumMismatchException` and blocks startup. Corrections ship as `017`.

**Constraints added**:

```sql
parsed_item_drafts_proposed_outcome_chk
    CHECK (proposed_outcome IN ('leg', 'item'))

parsed_item_drafts_transportation_mode_chk
    CHECK (transportation_mode IS NULL
           OR transportation_mode IN ('flight','train','bus','boat','car'))

parsed_item_drafts_travel_cost_chk
    CHECK (travel_cost IS NULL OR travel_cost >= 0)

parsed_item_drafts_recognition_state_chk
    CHECK (transport_recognition_state IN ('current','pending','unavailable'))
```

The mode list is duplicated here rather than shared, exactly as `014` duplicates it on `trip_legs`; `TransportationModes.All` is the single authority in code and the check is the database's own guard rail.

**The only backfill**:

```sql
UPDATE parsed_item_drafts
SET transport_recognition_state = 'pending'
WHERE review_status = 'pending_review'
  AND transport_recognition_state = 'current';
```

Guarded so it is a no-op on every restart after the first. It changes no traveler-visible value and touches no row that has created anything. **No statement in this script references `tracked_items` or `trip_legs`** (FR-043, FR-048).

**Validation rules**:

| Rule | Enforced where | Requirement |
| ---- | -------------- | ----------- |
| `proposed_outcome` is one of two values | Database check + `DraftOutcome` enum | FR-008 |
| `created_trip_leg_id` is set exactly when a draft is confirmed as a leg | `ConfirmDraftAsLeg` | FR-038 |
| `tracked_item_id` is set exactly when a draft is confirmed as an item | `ConfirmDraftEndpoint`, unchanged | FR-041 |
| A confirmed draft never has both ids | Mutually exclusive branches of one endpoint | FR-014 |
| `trip_leg_id`, when set, belongs to `trip_id` and can contain items | `UpdateDraftEndpoint` against `GetPlacementCandidateLegs` | FR-027 |
| A leg outcome requires end, end zone, origin, destination, and mode | `ConfirmDraftAsLeg` guards, then `TripLegValidator` | FR-029, FR-030 |

**State transitions**:

```text
                     ┌── outcome toggled (leg ⇄ item) any number of times ──┐
                     ▼                                                       │
pending_review ──────┴──── confirm as leg ──▶ confirmed   (writes created_trip_leg_id)
      │
      ├───────────────── confirm as item ──▶ confirmed   (writes tracked_item_id)
      │
      └───────────────── discard ─────────▶ discarded

transport_recognition_state:  pending ──re-recognize──▶ current | unavailable
                              current  (terminal)      unavailable (terminal)
```

Both review transitions remain terminal. A confirmed draft never returns to the queue, including when the leg it produced is later deleted — `ON DELETE SET NULL` nulls `created_trip_leg_id` and leaves `review_status = 'confirmed'` (FR-040), matching the reasoning `013_draft_item_traceability.sql` recorded for items.

### `trip_legs` (UNCHANGED)

Read and written through existing paths only. Listed because the asymmetry below is the reason FR-029 and FR-034 exist.

| Column | Type | Relevance |
| ------ | ---- | --------- |
| `end_at` / end time zone | NOT NULL | **A leg requires both.** `tracked_items` does not. This single difference is why a draft that confirms cleanly as an item may be unconfirmable as a leg |
| `origin` | NOT NULL for travel | `trip_legs_travel_origin_chk` from `014`. Hence FR-030 |
| `leg_kind`, `transportation_mode` | NOT NULL / checked | Written as `travel` plus the reviewed mode |
| `travel_cost`, `confirmation_code` | Optional | Both accepted from the draft |

The `tracked_items_leg_eligibility_trg` and `trip_legs_item_eligibility_trg` triggers installed by `014` are untouched and remain the final authority. A leg created by this feature is subject to them identically to a hand-entered one.

### `tracked_items` (UNCHANGED)

No column, constraint, trigger, or row is modified. Recorded explicitly because FR-043 makes the absence of a change a requirement rather than an omission.

## Contract changes

`src/TripPlanner.Contracts/EmailIngestion/EmailIngestionContracts.cs`

### `DraftOutcome` (NEW)

```csharp
public enum DraftOutcome { Item = 0, Leg = 1 }
```

`Item = 0` so the default value of an unset field is today's behaviour.

### `ParsedItemDraftDto` (MODIFIED)

| Field | Type | Purpose |
| ----- | ---- | ------- |
| `ProposedOutcome` | `DraftOutcome` | Which set of fields the review screen presents (FR-011, FR-023) |
| `Origin`, `Destination` | `string?` | Recognized route (FR-003) |
| `TransportationMode` | `string?` | One of `TransportationModes.All`, or null |
| `TravelCost` | `decimal?` | Numeric amount (FR-010) |
| `TravelCostCurrency` | `string?` | Display label only (D9) |
| `CreatedTripLegId` | `Guid?` | The leg this draft became (FR-038) |
| `TransportRecognitionState` | `DraftRecognitionState` | Tells the modal whether to call re-recognize (FR-045) |
| `Placement` | unchanged | Still computed per request; **suppressed by the UI when the outcome is Leg**, since a leg is not placed inside a leg |

### `UpdateParsedItemDraftRequest` (MODIFIED)

Gains `ProposedOutcome`, `Origin`, `Destination`, `TransportationMode`, `TravelCost`. Does **not** gain `TravelCostCurrency` — it is a recognized label, not a traveler-editable value. Every existing field is unchanged, so FR-042's "edits intact" holds for pre-existing drafts with no special case.

### `ConfirmParsedItemDraftResponse` (BREAKING)

```csharp
public sealed record ConfirmParsedItemDraftResponse(
    DraftOutcome Outcome,
    Guid TripId,
    Guid? TrackedItemId,     // set when Outcome is Item
    Guid? TripLegId,         // the leg an item was placed on; null for a leg outcome
    Guid? CreatedTripLegId); // set when Outcome is Leg
```

`TripLegId` and `CreatedTripLegId` mean different things and must not be merged: the first is where an item landed, the second is what was created. Breaking is acceptable on the same grounds feature 024 recorded — there are no external consumers and the compiler surfaces every call site.

### `DraftRecognitionState` (NEW)

```csharp
public enum DraftRecognitionState { Current = 0, Pending = 1, Unavailable = 2 }
```

### Unchanged contracts

`CreateTripLegRequest`, `UpdateTripLegRequest`, `TripLegShape`, `TripLegKinds`, `TransportationModes`, `TripLegEligibility`, `TripLegDto`, `CreateTrackedItemRequest`, `DraftPlacement`, `PlacementCandidate`, `DraftPlacementStatus`. This feature consumes all of them and changes none.

## Database access

`src/TripPlanner.Database/EmailIngestion/`

### Records (MODIFIED)

`ParsedItemDraftRecord`, `NewParsedItemDraft`, and `DraftUpdate` each gain the corresponding new fields. `NewParsedItemDraft` gains `ProposedOutcome`, `Origin`, `Destination`, `TransportationMode`, `TravelCost`, `TravelCostCurrency` — recognition supplies all six. `DraftUpdate` gains the five traveler-editable ones.

### `DraftRecognitionMerge` (NEW)

The payload of a re-recognition write. Carries the same six recognized fields plus the new `transport_recognition_state`. The SQL, not the C#, decides what survives (see below).

### `IParsedItemDraftRepository` (MODIFIED)

| Member | Change |
| ------ | ------ |
| `SetReviewStatusAsync` | Gains `DraftOutcome outcome` and `Guid? createdTripLegId`; the existing `trackedItemId` parameter stays |
| `MergeRecognitionAsync` | **NEW.** Applies a `DraftRecognitionMerge` under the FR-046 rule and returns the updated record |
| Everything else | Unchanged signature, wider row |

### SQL

| File | Change |
| ---- | ------ |
| `Commands/EmailIngestion/InsertParsedItemDraft.sql` | MODIFY — carry the six recognized fields and `proposed_outcome` |
| `Commands/EmailIngestion/UpdateParsedItemDraft.sql` | MODIFY — carry the five editable fields, and recompute `traveler_edited_fields` |
| `Commands/EmailIngestion/UpdateParsedItemDraftReviewStatus.sql` | MODIFY — `COALESCE` in `created_trip_leg_id` alongside `tracked_item_id`, and record `proposed_outcome` as confirmed |
| `Commands/EmailIngestion/MergeParsedItemDraftRecognition.sql` | **NEW** — the FR-046 merge |
| `Queries/EmailIngestion/GetParsedItemDrafts.sql` | MODIFY — project the new columns |
| `Queries/EmailIngestion/GetParsedItemDraftById.sql` | MODIFY — project the new columns |
| `Queries/EmailIngestion/GetPlacementCandidateLegs.sql` | **UNCHANGED** — it already excludes flight/train/bus/boat legs (FR-027) |

#### The merge rule, in SQL

The FR-046 guarantee is expressed once, in the statement that writes it, rather than in C# where each field would need remembering:

```sql
UPDATE parsed_item_drafts
SET origin = CASE WHEN origin IS NULL AND NOT ('origin' = ANY (traveler_edited_fields))
                  THEN @Origin ELSE origin END,
    -- ... identically for destination, transportation_mode,
    --     travel_cost, travel_cost_currency, item_type,
    --     and only when it is still null: end_timezone_id is deliberately excluded
    proposed_outcome = CASE WHEN NOT ('proposedOutcome' = ANY (traveler_edited_fields))
                            THEN @ProposedOutcome ELSE proposed_outcome END,
    transport_recognition_state = @State
WHERE parsed_item_draft_id = @ParsedItemDraftId
  AND user_id = @UserId
  AND review_status = 'pending_review'
RETURNING ...;
```

Two properties worth naming:

- **A field is filled only when it is both absent and unedited.** Absent alone is not enough, because a traveler who deliberately cleared a value would have it restored (research D7).
- **`end_timezone_id` and `end_local` are never merged.** Recognition may set them at first ingestion, but a re-run must not introduce an end the traveler has not seen — that is FR-034's prohibition applied to the one code path that could quietly violate it.

## In-memory models

`src/TripPlanner.Api/Features/EmailIngestion/`

### `TransportationModeInterpreter` (NEW)

`Interpret(string? mode, string? itemType) → string?` — returns a `TransportationModes` value or null. Exact names, the `itemType` values that imply a mode, and a fixed synonym table (research D1). Pure, static, provider-free, fully unit-testable.

### `DraftOutcomeClassifier` (NEW)

`Classify(RecognizedItem) → DraftOutcome` — `Leg` when `Interpret` returns a mode, `Item` otherwise. One expression, stated as a type so FR-036's consistency requirement has something to point at.

### `LegConfirmationGaps` (NEW)

The named-missing-detail result used by `ConfirmDraftAsLeg` and mirrored by the review screen's validation model. Checks in order: `tripId`, `title`, `startLocal`, `startTimeZoneId`, `endLocal`, `endTimeZoneId`, `origin`, `destination`, `transportationMode` — each mapping to a field name the UI already knows how to label.

### Unchanged

`DraftPlacementMatcher`, `EmailDeduplicationService`, `EmailSenderResolver`, `EmailAttachmentTextExtractor`. `RelayMessageProcessor` loses only its private `AssembleText`, which moves to a new static `EmailTextAssembler` so re-recognition provably assembles text the same way.

## Requirement coverage

| Requirement group | Where it lives |
| ----------------- | -------------- |
| FR-001 … FR-010 recognition | Prompt schema, `TransportationModeInterpreter`, `DraftOutcomeClassifier`, `ToDraft`, the new draft columns |
| FR-011 … FR-021 review & confirm | `DraftEditModal`, `ConfirmDraftAsLeg`, `TripLegValidator` (reused), `AuditOperations.TripLegCreate`, `ItineraryChangeKind.TripLegCreated` |
| FR-022 … FR-027 override | `proposed_outcome`, the outcome toggle, the carry-across notice, the existing `CanContainItems` filter |
| FR-028 … FR-034 missing details | `LegConfirmationGaps`, the leg-mode validation model, `DraftSuggestionRow` |
| FR-035 … FR-037 car rental | `TransportationModeInterpreter` (`car_rental` → Car), prompt wording for pickup/return |
| FR-038 … FR-041 traceability | `created_trip_leg_id`, `proposed_outcome`, unchanged `tracked_item_id` |
| FR-042 … FR-048 existing data | `transport_recognition_state`, `traveler_edited_fields`, `MergeParsedItemDraftRecognition.sql`, and the deliberate absence of any statement against `tracked_items` |
