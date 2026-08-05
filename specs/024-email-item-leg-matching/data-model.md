# Data Model: Matching Ingested Email Items to Trip Legs

**Feature**: 024-email-item-leg-matching | **Date**: 2026-08-05

The schema barely moves. One nullable column is added; everything else this feature needs already exists. The genuinely new modelling is in-memory only — placement is computed per request and never stored (see [research.md](research.md) D1).

## Persisted entities

### `parsed_item_drafts` (MODIFIED)

The review-queue row. Existing columns are unchanged.

| Column | Type | Notes |
| ------ | ---- | ----- |
| `parsed_item_draft_id` | `uuid` PK | |
| `inbox_email_id` | `uuid` NOT NULL | FK → `inbox_emails`, `ON DELETE CASCADE` |
| `user_id` | `text` NOT NULL | Owning traveler |
| `trip_id` | `uuid` NULL | FK → `trips`, `ON DELETE SET NULL`. **The traveler's explicit choice**, never a system guess |
| `trip_leg_id` | `uuid` NULL | The traveler's explicit choice. No FK today; left as-is |
| `item_type` | `text` NULL | Recognizer vocabulary (`flight`, `hotel`, `car_rental`, …), normalized on confirm |
| `title`, `location` | `text` NULL | |
| `start_local`, `end_local` | `timestamp` NULL | Local wall time, no offset |
| `start_timezone_id`, `end_timezone_id` | `text` NULL | IANA ids that give the local times meaning |
| `confirmation_code`, `notes` | `text` NULL | |
| `confidence` | `double precision` NOT NULL | Recognition confidence, not placement confidence |
| `review_status` | `text` NOT NULL | `pending_review` \| `confirmed` \| `discarded` |
| `created_at_utc` | `timestamptz` NOT NULL | |
| **`tracked_item_id`** | **`uuid` NULL** | **NEW.** FK → `tracked_items`, `ON DELETE SET NULL`. The item this draft became. Satisfies FR-025 |

**New script**: `Scripts/Schema/013_draft_item_traceability.sql`, idempotent via `ADD COLUMN IF NOT EXISTS`, consistent with how `005_trip_leg_items.sql` added columns.

**Validation rules**:

- `tracked_item_id` is null while `review_status` is `pending_review` or `discarded`
- `tracked_item_id` is set exactly when `review_status` transitions to `confirmed`
- `trip_leg_id`, when non-null, must belong to `trip_id` — enforced in `UpdateDraftEndpoint` (FR-018), not by a database constraint, since the pair spans two tables

**State transitions**:

```text
pending_review ──confirm──▶ confirmed   (writes tracked_item_id)
       │
       └───────discard───▶ discarded
```

Both transitions are terminal. A confirmed draft is never re-opened; correcting a mistake means editing the resulting item on the trip.

### `tracked_items` (UNCHANGED)

Listed because the feature depends on a property of it that is easy to misread.

| Column | Type | Notes |
| ------ | ---- | ----- |
| `trip_leg_id` | `uuid` **NULL** | Already nullable with `ON DELETE SET NULL`, added by `005_trip_leg_items.sql`. **No migration needed for FR-011** |

The constraint that forced a leg lived only in `TrackedItemValidator`, never in the database.

### `trip_legs` (UNCHANGED)

Supplies the travel window that defines coverage: `start_at`, `end_at` (nullable), plus the time zone ids surfaced on `TripLegDto` as `StartTimeZoneId` / `EndTimeZoneId`.

A leg with a null `end_at` has an open-ended window. The matcher treats such a leg as covering any instant at or after its start.

## In-memory models (not persisted)

These live in `TripPlanner.Api/Features/EmailIngestion` and exist only for the duration of a request.

### `DraftPlacement`

Returned alongside each draft in the list response.

| Field | Type | Meaning |
| ----- | ---- | ------- |
| `Status` | `DraftPlacementStatus` | Which of the five outcomes applies |
| `SuggestedTripId` | `Guid?` | Set only when `Status is Matched` |
| `SuggestedTripLegId` | `Guid?` | Set only when `Status is Matched` |
| `Candidates` | `IReadOnlyList<PlacementCandidate>` | Empty unless `Matched` or `Ambiguous` |

### `DraftPlacementStatus`

| Value | Condition | Drives which message |
| ----- | --------- | -------------------- |
| `Matched` | Exactly one leg across all editable trips contains the draft's timeframe | Pre-select trip and leg (FR-004) |
| `Ambiguous` | Two or more legs qualify | Offer all, pre-select none (FR-005) |
| `NoLegCovers` | No leg qualifies, but the timeframe falls inside at least one trip's own date range | "No leg covers these dates" (FR-009) |
| `OutsideTripDates` | No leg qualifies and the timeframe falls outside every editable trip's date range | Distinct message; the trip itself would have to change (FR-010) |
| `InsufficientData` | `start_local` or `start_timezone_id` is missing | "A start is required before placement can be resolved" |

`Ambiguous` deliberately spans trips as well as legs, because a traveler with overlapping trips gets the same treatment as one with overlapping legs.

### `PlacementCandidate`

| Field | Type | Purpose |
| ----- | ---- | ------- |
| `TripId` / `TripName` | `Guid` / `string` | Identify and label the trip |
| `TripLegId` / `LegTitle` | `Guid` / `string` | Identify and label the leg |
| `LegStart` / `LegEnd` | `DateTimeOffset` / `DateTimeOffset?` | The window that matched, shown as the reason (FR-007) |

## Query

### `GetPlacementCandidateLegs.sql` (NEW)

`Scripts/Queries/EmailIngestion/GetPlacementCandidateLegs.sql`

Returns every leg on every trip the caller can **edit**, with the trip's own date range for the `OutsideTripDates` determination. Reuses the accessible-trips CTE shape from `GetTripsPage.sql` — owner rows `UNION ALL` share rows matched on `member_user_id` or lowercased `member_email` — filtered to owner or collaborator access, excluding viewers (FR-003).

Parameters: `@OwnerUserId`, `@CallerEmail`.

Returns per row: trip id, trip name, trip start/end date, leg id, leg title, `start_at`, `end_at`, and the leg's time zone ids.

One call per draft-list request serves every draft; the matcher evaluates each draft against the same in-memory result.

## Contract changes

| Type | Change | Requirement |
| ---- | ------ | ----------- |
| `CreateTrackedItemRequest.TripLegId` | `Guid` → `Guid?` | FR-011, FR-021 |
| `UpdateTrackedItemRequest.TripLegId` | `Guid` → `Guid?` | FR-011, FR-021 |
| `ConfirmParsedItemDraftResponse.TripLegId` | `Guid` → `Guid?` | An unassigned confirmation has no leg to report |
| `ParsedItemDraftDto` | Add `Placement` | FR-004, FR-005, FR-007, FR-009, FR-010 |
| `UpdateParsedItemDraftRequest` | Unchanged — already carries all eleven editable fields | FR-016, FR-017 |

All are breaking. That is acceptable: the product owner confirmed there are no external consumers, and the compiler will surface every call site.

## Validation rule changes

`TrackedItemValidator.ValidateCore` — three rejections removed, one kept and made conditional:

| Current behavior | New behavior | Why |
| ---------------- | ------------ | --- |
| Fail when `trip.Legs.Count == 0` | **Removed** | A trip with no legs can now hold unassigned items (FR-014) |
| Fail when `tripLegId == Guid.Empty` | **Removed** | Null now means "deliberately unassigned" (FR-011) |
| Fail when the leg is not on the trip | **Kept** — applies when a leg is supplied | Still a genuine error (FR-018) |
| Fail when start/end fall outside the leg window | **Kept, conditional on a leg being supplied** | FR-020; Q3 chose strict refusal over warn-or-widen |

Every other check — item type, title, time zones, end-on-or-after-start, color, field lengths, estimated cost — is untouched and continues to apply to both paths.
