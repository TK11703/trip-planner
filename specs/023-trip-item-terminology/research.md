# Research: Trip Leg Item Terminology

**Feature**: 023-trip-item-terminology
**Date**: 2026-08-03
**Input**: [spec.md](./spec.md)

This document resolves the open technical questions behind a full-stack rename of the generic "event" concept to "item". Every decision below was verified against the current source, not assumed.

---

## R-001: Which occurrences of "event" are actually in scope

**Decision**: Rename only the *first* of the three distinct senses of "event" found in the repository.

| Sense | Meaning | Examples | Action |
|-------|---------|----------|--------|
| 1. Trip leg child | The entry that belongs to a trip leg | `parsed_event_drafts`, `RecognizedEvent`, `EventCountLabel`, "Add event", "3 events" | **RENAME to item** |
| 2. Item type value | One of four values in the item-type field | `TrackedItemTypes.Event`, the stored string `'event'`, the `<option value="event">Event</option>` label, the type badge and icon | **KEEP** |
| 3. Unrelated domain events | Security audit records and notification deduplication keys | `audit_events` table, `AuditEvent` contract, `InsertAuditEvent.sql`, `source_event_key` column, `notifications_recipient_event_uq` | **KEEP — out of scope** |

**Rationale**: The user's stated reason for the rename is that "the leg items have an item type field which categorized the item, so the name event was too specific". That reasoning applies only to a leg's children. A security audit record genuinely *is* an event, and a notification deduplication key genuinely refers to *the event that triggered the notification* — neither is a trip leg child, and neither participates in the four-value type field. Renaming them would be unrelated churn that touches the audit and notification schemas for no clarity gain.

**Alternatives considered**:
- *Rename literally every occurrence of "event"*: rejected. It would rename `audit_events` and `source_event_key`, forcing two extra table/column migrations and an audit contract break, while making the audit code read *worse* ("audit item" is meaningless).
- *Also rename sense 3 in a follow-up feature*: not planned. Sense 3 is correct as written; there is nothing to follow up.

**Spec impact**: FR-017 and SC-007 were written as "the only occurrences of 'event' anywhere in the product" and have been narrowed to the trip-leg-child sense to match this decision.

---

## R-002: How the database applies schema scripts

**Decision**: The schema system is **desired-state, not tracked migrations**. Every `.sql` file in `Scripts/Schema/` is executed on every application start, in case-insensitive filename order, with no record of what has already run.

**Evidence**:
- [DatabaseInitializer.cs](src/TripPlanner.Database/Initialization/DatabaseInitializer.cs) calls `sql.GetAllInDirectory("Schema")` and executes every returned script in sequence on each `InitializeAsync`.
- [SqlFileProvider.cs](src/TripPlanner.Database/Sql/SqlFileProvider.cs) orders with `Directory.GetFiles(directory, "*.sql").OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)`.
- There is no applied-migrations table anywhere in `Scripts/Schema/`.
- Scripts are written idempotently (`CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`), which is what makes repeated execution safe.

**Consequences that drive the plan**:
1. Renaming a schema *file* is harmless — file names have no persistence meaning. Highest existing number is `011`.
2. A naive rename is **not** safe. If `010_email_ingestion.sql` is edited to create `parsed_item_drafts`, an existing database keeps its populated `parsed_event_drafts` and gains an empty `parsed_item_drafts`. Data would appear lost.
3. A reconciling script placed *after* `010` cannot simply `ALTER TABLE ... RENAME TO`, because by then `010` has already created the empty target in the same startup pass.

---

## R-003: Strategy for renaming `parsed_event_drafts`

**Decision**: Edit `010_email_ingestion.sql` in place to the new names, and add `012_parsed_item_drafts_rename.sql` that **backfills from the legacy table and drops it**, guarded so it is a no-op once complete.

**Shape of the reconciling script**:

```sql
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables
               WHERE table_schema = 'public' AND table_name = 'parsed_event_drafts') THEN
        INSERT INTO parsed_item_drafts (parsed_item_draft_id, inbox_email_id, user_id, trip_id,
                                        trip_leg_id, item_type, title, location, start_local,
                                        start_timezone_id, end_local, end_timezone_id,
                                        confirmation_code, notes, confidence, review_status,
                                        created_at_utc)
        SELECT parsed_event_draft_id, inbox_email_id, user_id, trip_id,
               trip_leg_id, event_type, title, location, start_local,
               start_timezone_id, end_local, end_timezone_id,
               confirmation_code, notes, confidence, review_status,
               created_at_utc
        FROM parsed_event_drafts
        ON CONFLICT (parsed_item_draft_id) DO NOTHING;

        DROP TABLE parsed_event_drafts;
    END IF;
END $$;
```

**Why this shape**:
- On a **fresh** database: `010` creates `parsed_item_drafts`; `012` finds no legacy table and does nothing. Correct.
- On an **existing** database: `010` creates the new empty `parsed_item_drafts`; `012` copies every legacy row across, preserving primary keys, then drops the legacy table. Correct, and satisfies FR-015 (all rows preserved, no traveler action).
- On **every subsequent start**: `010` no-ops (`IF NOT EXISTS`); `012` finds no legacy table and no-ops. Stable.
- `ON CONFLICT DO NOTHING` makes the copy safe even if the script is interrupted and re-run.

**Alternatives considered**:
- *`ALTER TABLE parsed_event_drafts RENAME TO parsed_item_drafts` in a `012` script*: rejected. `010` runs first in the same pass and has already created the empty target, so the rename fails on the very first upgrade start.
- *Number the rename below `010` so it runs first*: rejected. It inverts the chronological meaning of the numbering and is fragile against future insertions.
- *Leave `010` untouched and only add `012`*: rejected. Because `010` re-runs on every start, it would recreate an empty `parsed_event_drafts` forever after the rename, leaving a permanent stray table.
- *Drop and recreate*: rejected. Loses pending drafts.

**Note**: The check constraint and index inside `010` are renamed with the table. The legacy database's constraint/index objects are dropped along with the legacy table by `DROP TABLE`.

---

## R-004: Renaming SQL command/query files

**Decision**: Rename the files and update the lookup strings in the same commit.

**Evidence**: SQL is loaded by literal relative path, e.g. [ParsedEventDraftRepository.cs](src/TripPlanner.Database/EmailIngestion/ParsedEventDraftRepository.cs) line 21 does `_sql.Get("Commands/EmailIngestion/InsertParsedEventDraft.sql")`. These are strings, so the compiler will **not** catch a mismatch — a stale lookup fails at runtime, not build time.

**Consequence**: After renaming SQL files, run a clean rebuild. `.sql` files are copied to the output directory (confirmed by the stale copies under `src/TripPlanner.Database/bin/Debug/net10.0/Scripts/`), so an incremental build can leave both the old and new file in `bin`, masking a broken lookup during local testing.

---

## R-005: The LLM prompt and its response envelope must change together

**Decision**: Rename the JSON contract used with the language model — `"events"` → `"items"` and `"eventType"` → `"itemType"` — in the prompt text and the deserialization types as a single atomic edit.

**Evidence**: [EmailParserService.cs](src/TripPlanner.Api/Features/EmailIngestion/EmailParserService.cs) lines 74, 76, and 86 instruct the model to return `{"events":[...]}` with an `eventType` field, and `RecognizedEventEnvelope.Events` / `RecognizedEvent.EventType` deserialize exactly those names by convention.

**Risk**: If the prompt is changed but the types are not (or vice versa), deserialization silently yields an empty list — no exception, no parsed drafts, and the failure looks like "the model found nothing". This is the single highest-risk edit in the feature.

**Mitigation**: Change both in one task, and cover it with a test that deserializes a literal `{"items":[{"itemType":"hotel", ...}]}` payload and asserts a non-empty result.

---

## R-006: API contract breakage

**Decision**: Rename request/response property names outright, with no versioning, compatibility shim, or deprecation window.

**Rationale**: The user confirmed the API has no consumers outside this product (FR-014). The web application and the API ship from the same solution and deploy together.

**Affected wire fields** (all in the email-ingestion feature):
- `parsedEventDraftId` → `parsedItemDraftId`
- `eventType` → `itemType`

**Explicitly unaffected**: the trip-item CRUD surface is already neutral — routes are `POST/PUT/DELETE /api/trips/{tripId}/items[/{itemId}]`, and the payloads (`CreateTrackedItemRequest`, `UpdateTrackedItemRequest`, `TrackedItemDto`) already use `itemType`. No route changes anywhere in the feature, which is what keeps FR-011 (URLs keep working) trivially satisfied.

---

## R-007: Naming conventions for the new identifiers

**Decision**: Substitute `Event` → `Item` and `event` → `item` in place, keeping every surrounding word. Do not restructure names.

**Rationale**: The codebase already has an established `Item` vocabulary — `TrackedItemDto`, `TrackedItemTypes`, `CreateTrackedItemRequest`, `TripItemRepository`, `TimelineItem`, the `tracked_items.item_type` column, and the `/items` routes. A literal substitution lands the new names directly inside that existing family with no collisions, because every proposed new name is currently unused.

**Collision check performed**: no existing type, table, column, route segment, or CSS class already holds any of the proposed new names. `ParsedItemDraftDto`, `RecognizedItem`, `IItemRecognizer`, `parsed_item_drafts`, and `.tp-print-item` are all free.

**One name needs care**: `RecognitionResult` in the email parser is generic enough already; it is renamed to `ItemRecognitionResult` only if it reads ambiguously beside `RecognizedItem`. This is cosmetic and low value — deferred rather than forced.

---

## R-008: Singular and plural rendering

**Decision**: Keep the existing count-label helper method and change only its output strings; rename the method itself to match.

**Evidence**: [TripTimeline.razor](src/TripPlanner.Web/Components/Timeline/TripTimeline.razor) already has an `EventCountLabel(...)` helper returning `"0 events"` / `"1 event"` / `"{n} events"`. Renaming it to `ItemCountLabel` and changing the three literals satisfies FR-003 with no new abstraction.

**One placeholder plural must be removed**: [TripTimeline.razor](src/TripPlanner.Web/Components/Timeline/TripTimeline.razor) line 32 renders `"@_timeline.UnassignedItems.Count event(s) are not related to a trip leg."`. The spec forbids `item(s)` forms, so this becomes a proper singular/plural sentence including the verb agreement ("1 item is" / "N items are").

---

## R-009: Verification approach

**Decision**: Prove completion with a repository-wide search gate rather than by inspection.

**The gate**: a case-insensitive search for `event` across `src/`, `tests/`, and `infra/`, excluding `bin/`, `obj/`, and `wwwroot/lib/`, must return only:
1. Sense-2 hits — `TrackedItemTypes.Event`, the `'event'` type value in the check constraint, the `Event` option label, and type-value test data.
2. Sense-3 hits — audit and notification-deduplication identifiers listed in R-001.
3. Framework and DOM hits — `pointer-events`, `addEventListener`, `@onclick` plumbing, Bootstrap's bundled JavaScript.

**Rationale**: The change spans roughly 143 occurrences across seven projects. A reviewer cannot reliably confirm completeness by reading diffs; a mechanical gate can. This directly implements SC-007.

**Excluded from the gate**: `wwwroot/lib/bootstrap/**` is vendored third-party code and is never edited.

---

## R-010: Delivery order

**Decision**: Deliver in the spec's user-story priority order, with one deviation — the database and contract rename (US5) must land **before** the surfaces that depend on the renamed types.

**Sequence**:
1. **Foundation** — database script rename plus `012` reconciliation, `TripPlanner.Contracts` renames, `TripPlanner.Database` repository renames. Nothing traveler-visible; the solution must build and all existing tests must pass.
2. **US1 (P1)** — planning surfaces: timeline, itinerary summary, empty states, printable trip, `.tp-print-item` CSS.
3. **US2 (P2)** — add/edit form labels, help text, and validator messages.
4. **US3 (P3)** — notification kinds and templates, email-review screens, the LLM prompt and envelope.
5. **US4 (P4)** — FAQ, About, profile help text.
6. **US5 (P5) closeout** — remaining internal method/property renames in the Web project, test renames, and the R-009 search gate.

**Rationale**: US5 as specified is partly a precondition (renaming types other layers consume) and partly a closeout (proving no generic "event" survives). Splitting it that way keeps every intermediate state compiling, which is what makes the story-by-story delivery in the constitution's "small vertical slice" principle workable here. Steps 2 through 5 are independently shippable and independently testable, exactly as the spec's independent tests describe.
